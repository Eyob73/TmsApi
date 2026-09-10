using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.DTOs.Enrollments;
using TmsApi.Application.Hubs;
using TmsApi.Application.Interfaces;
using TmsApi.Api.Hubs;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly TmsDbContext _context;
    private readonly IHubContext<TmsHub, ITmsHubClient> _hubContext;
    private readonly ILogger<EnrollmentService> _logger;

    private readonly INotificationService _notificationService;

    public EnrollmentService(
        TmsDbContext context,
        IHubContext<TmsHub, ITmsHubClient> hubContext,
        ILogger<EnrollmentService> logger,
        INotificationService notificationService
    )
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<int> GetOrCreateStudentForUserAsync(
        string userId,
        string email,
        string firstName,
        string lastName,
        CancellationToken ct = default
    )
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (student != null)
            return student.Id;

        // Try matching by email
        if (!string.IsNullOrWhiteSpace(email))
        {
            student = await _context.Students
                .FirstOrDefaultAsync(s => s.Email != null && s.Email.ToLower() == email.ToLower(), ct);

            if (student != null)
            {
                student.UserId = userId;
                await _context.SaveChangesAsync(ct);
                return student.Id;
            }
        }

        // Create new Student profile
        var fullName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = email.Split('@')[0];

        var regNum = $"STU-{Math.Abs(userId.GetHashCode()) % 9000 + 1000:D4}";

        // Ensure registration number is unique
        while (await _context.Students.AnyAsync(s => s.RegistrationNumber == regNum, ct))
        {
            regNum = $"STU-{Random.Shared.Next(1000, 9999):D4}";
        }

        student = new Student
        {
            UserId = userId,
            Email = email,
            Name = fullName,
            RegistrationNumber = regNum,
            GPA = 3.50m,
            IsActive = true
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Created new Student profile {StudentId} ({RegistrationNumber}) for user {UserId}", student.Id, student.RegistrationNumber, userId);

        return student.Id;
    }

    public async Task<IReadOnlyList<AvailableCourseDto>> GetAvailableCoursesAsync(
        int studentId,
        CancellationToken ct = default
    )
    {
        var now = DateTime.UtcNow;

        var courses = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Department)
            .Include(c => c.Program)
            .Where(c => !c.IsDeleted && c.IsPublished)
            .OrderBy(c => c.CourseCode)
            .ToListAsync(ct);

        var courseIds = courses.Select(c => c.Id).ToList();

        // Active enrollments per course
        var activeEnrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => courseIds.Contains(e.CourseId) && !e.IsArchived
                && (e.Status == EnrollmentStatus.Pending || e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Completed))
            .GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CourseId, g => g.Count, ct);

        // Student's existing enrollments
        var studentEnrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => courseIds.Contains(e.CourseId) && e.StudentId == studentId && !e.IsArchived)
            .GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Latest = g.OrderByDescending(x => x.EnrolledAt).First() })
            .ToDictionaryAsync(g => g.CourseId, g => g.Latest.Status.ToString(), ct);

        var result = new List<AvailableCourseDto>();

        foreach (var c in courses)
        {
            var enrolledCount = activeEnrollments.GetValueOrDefault(c.Id, 0);
            var maxCap = c.MaxCapacity ?? 30;
            var availableSeats = Math.Max(0, maxCap - enrolledCount);

            var isWindowOpen = c.IsEnrollmentOpen
                && (!c.EnrollmentStartDate.HasValue || c.EnrollmentStartDate <= now)
                && (!c.EnrollmentEndDate.HasValue || c.EnrollmentEndDate >= now);

            string availabilityStatus;
            if (!c.IsActive || !isWindowOpen)
            {
                availabilityStatus = "Closed";
            }
            else if (enrolledCount >= maxCap)
            {
                availabilityStatus = "Full";
            }
            else if (enrolledCount >= (int)(maxCap * 0.8))
            {
                availabilityStatus = "Almost Full";
            }
            else
            {
                availabilityStatus = "Available";
            }

            studentEnrollments.TryGetValue(c.Id, out var studentStatus);

            var hasActiveEnrollment = studentStatus == "Pending" || studentStatus == "Approved" || studentStatus == "Completed";
            var canEnroll = !hasActiveEnrollment && availabilityStatus != "Closed" && availabilityStatus != "Full";

            result.Add(new AvailableCourseDto(
                c.Id,
                c.CourseCode,
                c.CourseName,
                c.Description,
                c.Credits,
                c.Department?.Name,
                c.Program?.Name,
                c.Level,
                c.Semester,
                c.CourseType,
                c.DurationHours,
                c.InstructorId,
                c.MaxCapacity,
                enrolledCount,
                availableSeats,
                c.IsEnrollmentOpen,
                c.EnrollmentStartDate,
                c.EnrollmentEndDate,
                availabilityStatus,
                studentStatus,
                canEnroll
            ));
        }

        return result;
    }

    public async Task<EnrollmentResponseDto> RequestEnrollmentAsync(
        int studentId,
        int courseId,
        CancellationToken ct = default
    )
    {
        var student = await _context.Students.FindAsync([studentId], ct);
        if (student == null || student.IsDeleted)
            throw new KeyNotFoundException($"Student with ID {studentId} not found.");

        if (!student.IsActive)
            throw new InvalidOperationException("Student account is inactive. Cannot enroll in courses.");

        var course = await _context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);

        if (course == null)
            throw new KeyNotFoundException($"Course with ID {courseId} not found.");

        if (!course.IsPublished || !course.IsActive)
            throw new InvalidOperationException("Course is not available for enrollment.");

        var now = DateTime.UtcNow;
        if (!course.IsEnrollmentOpen || (course.EnrollmentStartDate.HasValue && course.EnrollmentStartDate > now) || (course.EnrollmentEndDate.HasValue && course.EnrollmentEndDate < now))
            throw new InvalidOperationException("Course enrollment period is currently closed.");

        // Duplicate active enrollment check
        var hasActive = await _context.Enrollments.AnyAsync(e =>
            e.StudentId == studentId
            && e.CourseId == courseId
            && !e.IsArchived
            && (e.Status == EnrollmentStatus.Pending || e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Completed),
            ct
        );

        if (hasActive)
            throw new InvalidOperationException("You already have a pending or active enrollment for this course.");

        // Capacity check
        var maxCap = course.MaxCapacity ?? 30;
        var activeCount = await _context.Enrollments.CountAsync(e =>
            e.CourseId == courseId
            && !e.IsArchived
            && (e.Status == EnrollmentStatus.Pending || e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Completed),
            ct
        );

        if (activeCount >= maxCap)
            throw new InvalidOperationException("Course maximum capacity has been reached.");

        var enrollment = new Enrollment
        {
            StudentId = studentId,
            CourseId = courseId,
            Status = EnrollmentStatus.Pending,
            EnrolledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Enrollment request created: ID {EnrollmentId} for Student {StudentId} in Course {CourseId}", enrollment.Id, studentId, courseId);

        // SignalR notifications
        try
        {
            await _hubContext.Clients.All.ReceiveEnrollmentCreated(enrollment.Id, studentId, courseId, "Pending");
            await _hubContext.Clients.All.ReceiveEnrollmentStatusUpdated(enrollment.Id.ToString(), "Pending");

            // Persist notification for admins
            var admins = await _context.Users
                .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                .Join(_context.Roles, x => x.ur.RoleId, r => r.Id, (x, r) => new { x.u, RoleName = r.Name })
                .Where(x => x.RoleName == "Admin")
                .Select(x => x.u.Id)
                .ToListAsync(ct);

            foreach (var adminId in admins)
            {
                await _notificationService.CreateAsync(
                    adminId,
                    "New Enrollment Request",
                    $"{student.Name} requested enrollment in {course.CourseName}.",
                    "Enrollment",
                    enrollment.Id.ToString(),
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR event for enrollment {EnrollmentId}", enrollment.Id);
        }

        return new EnrollmentResponseDto(
            enrollment.Id,
            studentId,
            student.Name,
            course.Id,
            course.CourseCode,
            course.CourseName,
            enrollment.Status.ToString(),
            enrollment.EnrolledAt,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null
        );
    }

    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetMyEnrollmentsAsync(
        int studentId,
        CancellationToken ct = default
    )
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId && !e.IsArchived)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.StudentId,
                e.Student.Name,
                e.CourseId,
                e.Course.CourseCode,
                e.Course.CourseName,
                e.Status.ToString(),
                e.EnrolledAt,
                e.ApprovedDate,
                e.ApprovedBy,
                e.RejectedDate,
                e.RejectedBy,
                e.RejectionReason,
                e.CancellationDate,
                e.CancelledBy,
                e.CompletionDate,
                e.IsArchived,
                e.Grade
            ))
            .ToListAsync(ct);
    }

    public async Task<EnrollmentDetailsDto?> GetMyEnrollmentByIdAsync(
        int studentId,
        int id,
        CancellationToken ct = default
    )
    {
        var e = await _context.Enrollments
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Course)
                .ThenInclude(c => c.Department)
            .Include(x => x.Course)
                .ThenInclude(c => c.Program)
            .FirstOrDefaultAsync(x => x.Id == id && x.StudentId == studentId, ct);

        return e == null ? null : MapToDetailsDto(e);
    }

    public async Task<EnrollmentResponseDto> CancelEnrollmentAsync(
        int studentId,
        int id,
        string cancelledBy,
        CancellationToken ct = default
    )
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (enrollment == null)
            throw new KeyNotFoundException($"Enrollment with ID {id} not found.");

        if (enrollment.StudentId != studentId && cancelledBy != "Admin")
            throw new UnauthorizedAccessException("You are not authorized to cancel this enrollment.");

        enrollment.Cancel(cancelledBy);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Enrollment {EnrollmentId} cancelled by {CancelledBy}", id, cancelledBy);

        try
        {
            await _hubContext.Clients.All.ReceiveEnrollmentCancelled(id);
            await _hubContext.Clients.All.ReceiveEnrollmentStatusUpdated(id.ToString(), "Cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR cancellation for enrollment {EnrollmentId}", id);
        }

        return MapToResponseDto(enrollment);
    }

    public async Task<PagedResponse<EnrollmentListItemDto>> GetPagedEnrollmentsAsync(
        EnrollmentFilterRequest filter,
        CancellationToken ct = default
    )
    {
        var query = _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Course)
            .AsQueryable();

        if (!filter.IncludeArchived)
        {
            query = query.Where(e => !e.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<EnrollmentStatus>(filter.Status, true, out var statusEnum))
        {
            query = query.Where(e => e.Status == statusEnum);
        }

        if (filter.CourseId.HasValue)
        {
            query = query.Where(e => e.CourseId == filter.CourseId.Value);
        }

        if (filter.StudentId.HasValue)
        {
            query = query.Where(e => e.StudentId == filter.StudentId.Value);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(e => e.EnrolledAt >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(e => e.EnrolledAt <= filter.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(e =>
                e.Student.Name.ToLower().Contains(search)
                || e.Student.RegistrationNumber.ToLower().Contains(search)
                || e.Course.CourseCode.ToLower().Contains(search)
                || e.Course.CourseName.ToLower().Contains(search)
            );
        }

        query = filter.SortBy?.ToLower() switch
        {
            "studentname" => filter.Descending ? query.OrderByDescending(e => e.Student.Name) : query.OrderBy(e => e.Student.Name),
            "coursecode" => filter.Descending ? query.OrderByDescending(e => e.Course.CourseCode) : query.OrderBy(e => e.Course.CourseCode),
            "status" => filter.Descending ? query.OrderByDescending(e => e.Status) : query.OrderBy(e => e.Status),
            _ => filter.Descending ? query.OrderByDescending(e => e.EnrolledAt) : query.OrderBy(e => e.EnrolledAt),
        };

        var totalCount = await query.CountAsync(ct);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EnrollmentListItemDto(
                e.Id,
                e.StudentId,
                e.Student.Name,
                e.Student.RegistrationNumber,
                e.CourseId,
                e.Course.CourseCode,
                e.Course.CourseName,
                e.Status.ToString(),
                e.EnrolledAt,
                e.ApprovedBy ?? e.RejectedBy,
                e.ApprovedDate ?? e.RejectedDate,
                e.RejectionReason,
                e.IsArchived,
                e.Grade
            ))
            .ToListAsync(ct);

        return new PagedResponse<EnrollmentListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<EnrollmentDetailsDto?> GetEnrollmentByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _context.Enrollments
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Course)
                .ThenInclude(c => c.Department)
            .Include(x => x.Course)
                .ThenInclude(c => c.Program)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return e == null ? null : MapToDetailsDto(e);
    }

    public async Task<EnrollmentResponseDto> ApproveEnrollmentAsync(
        int id,
        string approverId,
        CancellationToken ct = default
    )
    {
        // Concurrency and capacity safe execution within a transaction
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            var isInMemory = _context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;
            await using var tx = isInMemory ? null : await _context.Database.BeginTransactionAsync(ct);

            var enrollment = await _context.Enrollments
                .Include(e => e.Course)
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.Id == id, ct);

            if (enrollment == null)
                throw new KeyNotFoundException($"Enrollment with ID {id} not found.");

            if (enrollment.Status != EnrollmentStatus.Pending)
                throw new InvalidOperationException($"Cannot approve enrollment with current status '{enrollment.Status}'. Only Pending enrollments can be approved.");

            var course = enrollment.Course;
            var maxCap = course.MaxCapacity ?? 30;

            // Strict capacity recount of approved seats
            var approvedCount = await _context.Enrollments
                .CountAsync(e => e.CourseId == course.Id && e.Status == EnrollmentStatus.Approved && !e.IsArchived, ct);

            if (approvedCount >= maxCap)
                throw new InvalidOperationException($"Cannot approve enrollment: Course '{course.CourseCode}' is already at full capacity ({maxCap}/{maxCap} seats).");

            enrollment.Approve(approverId);
            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }

            _logger.LogInformation("Enrollment {EnrollmentId} approved by {ApproverId}", id, approverId);

            try
            {
                await _hubContext.Clients.All.ReceiveEnrollmentApproved(id);
                await _hubContext.Clients.All.ReceiveEnrollmentStatusUpdated(id.ToString(), "Approved");

                // Notify the student
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == enrollment.StudentId, ct);
                if (student != null)
                {
                    await _notificationService.CreateAsync(
                        student.UserId,
                        "Enrollment Approved",
                        $"Your enrollment in {course.CourseName} has been approved.",
                        "Enrollment",
                        id.ToString(),
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast SignalR approval for enrollment {EnrollmentId}", id);
            }

            return MapToResponseDto(enrollment);
        });
    }

    public async Task<EnrollmentResponseDto> RejectEnrollmentAsync(
        int id,
        string? reason,
        string reviewerId,
        CancellationToken ct = default
    )
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (enrollment == null)
            throw new KeyNotFoundException($"Enrollment with ID {id} not found.");

        if (enrollment.Status != EnrollmentStatus.Pending)
            throw new InvalidOperationException($"Cannot reject enrollment with current status '{enrollment.Status}'. Only Pending enrollments can be rejected.");

        enrollment.Reject(reason ?? "Enrollment request rejected.", reviewerId);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Enrollment {EnrollmentId} rejected by {ReviewerId}. Reason: {Reason}", id, reviewerId, reason);

        try
        {
            await _hubContext.Clients.All.ReceiveEnrollmentRejected(id, reason);
            await _hubContext.Clients.All.ReceiveEnrollmentStatusUpdated(id.ToString(), "Rejected");

            // Notify the student
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == enrollment.StudentId, ct);
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == enrollment.CourseId, ct);
            if (student != null)
            {
                await _notificationService.CreateAsync(
                    student.UserId,
                    "Enrollment Rejected",
                    $"Your enrollment in {course?.CourseName ?? "a course"} was rejected.{(reason != null ? $" Reason: {reason}" : "")}",
                    "Enrollment",
                    id.ToString(),
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR rejection for enrollment {EnrollmentId}", id);
        }

        return MapToResponseDto(enrollment);
    }

    public async Task<EnrollmentResponseDto> ArchiveEnrollmentAsync(
        int id,
        string userId,
        CancellationToken ct = default
    )
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (enrollment == null)
            throw new KeyNotFoundException($"Enrollment with ID {id} not found.");

        enrollment.Archive();
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Enrollment {EnrollmentId} archived by {UserId}", id, userId);

        return MapToResponseDto(enrollment);
    }

    // Legacy and backward compatibility methods
    public async Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct = default)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Status = EnrollmentStatus.Approved
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(courseId, enrollment.Id, ct))!;
    }

    public async Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct = default)
    {
        var e = await _context.Enrollments
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.Id == id && x.CourseId == courseId, ct);

        return e == null ? null : MapToResponseDto(e);
    }

    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(int id)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Course)
            .Where(e => e.CourseId == id)
            .Select(e => MapToResponseDto(e))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Course)
            .Select(e => MapToResponseDto(e))
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!int.TryParse(id, out var enrollmentId))
            return false;

        var enrollment = await _context.Enrollments.FindAsync(enrollmentId);
        if (enrollment == null)
            return false;

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<EnrollmentResponseDto?> GetByCourseAsync(int courseId, CancellationToken ct = default)
    {
        var e = await _context.Enrollments
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.CourseId == courseId, ct);

        return e == null ? null : MapToResponseDto(e);
    }

    public async Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.StudentId == studentId && e.Course.CourseCode == courseCode && !e.IsArchived
                && (e.Status == EnrollmentStatus.Pending || e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Completed), ct);
    }

    private static EnrollmentResponseDto MapToResponseDto(Enrollment e)
    {
        return new EnrollmentResponseDto(
            e.Id,
            e.StudentId,
            e.Student?.Name ?? string.Empty,
            e.CourseId,
            e.Course?.CourseCode ?? string.Empty,
            e.Course?.CourseName ?? string.Empty,
            e.Status.ToString(),
            e.EnrolledAt,
            e.ApprovedDate,
            e.ApprovedBy,
            e.RejectedDate,
            e.RejectedBy,
            e.RejectionReason,
            e.CancellationDate,
            e.CancelledBy,
            e.CompletionDate,
            e.IsArchived,
            e.Grade
        );
    }

    private static EnrollmentDetailsDto MapToDetailsDto(Enrollment e)
    {
        return new EnrollmentDetailsDto
        {
            Id = e.Id,
            Status = e.Status.ToString(),
            EnrollmentDate = e.EnrolledAt,
            ApprovedDate = e.ApprovedDate,
            ApprovedBy = e.ApprovedBy,
            RejectedDate = e.RejectedDate,
            RejectedBy = e.RejectedBy,
            RejectionReason = e.RejectionReason,
            CancellationDate = e.CancellationDate,
            CancelledBy = e.CancelledBy,
            CompletionDate = e.CompletionDate,
            IsArchived = e.IsArchived,
            Grade = e.Grade,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            Student = new StudentDetailInfoDto
            {
                Id = e.Student.Id,
                RegistrationNumber = e.Student.RegistrationNumber,
                Name = e.Student.Name,
                Email = e.Student.Email,
                GPA = e.Student.GPA,
                IsActive = e.Student.IsActive
            },
            Course = new CourseDetailInfoDto
            {
                Id = e.Course.Id,
                CourseCode = e.Course.CourseCode,
                CourseName = e.Course.CourseName,
                Description = e.Course.Description,
                Credits = e.Course.Credits,
                DepartmentName = e.Course.Department?.Name,
                ProgramName = e.Course.Program?.Name,
                Level = e.Course.Level,
                Semester = e.Course.Semester,
                CourseType = e.Course.CourseType,
                DurationHours = e.Course.DurationHours,
                Status = e.Course.Status,
                MaxCapacity = e.Course.MaxCapacity,
                EnrolledCount = e.Course.Enrollments?.Count(x => !x.IsArchived && x.Status == EnrollmentStatus.Approved) ?? 0,
                AvailableSeats = e.Course.MaxCapacity.HasValue
                    ? Math.Max(0, e.Course.MaxCapacity.Value - (e.Course.Enrollments?.Count(x => !x.IsArchived && x.Status == EnrollmentStatus.Approved) ?? 0))
                    : null,
                InstructorId = e.Course.InstructorId
            }
        };
    }
}
