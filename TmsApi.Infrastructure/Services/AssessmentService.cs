using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Grading;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class AssessmentService : IAssessmentService
{
    private readonly TmsDbContext _dbContext;
    private readonly GradingService _gradingService;

    public AssessmentService(TmsDbContext dbContext, GradingService gradingService)
    {
        _dbContext = dbContext;
        _gradingService = gradingService;
    }

    public async Task<IReadOnlyList<AssessmentDto>> GetAssessmentsAsync(int? courseId, CancellationToken ct)
    {
        var query = _dbContext.Assessments
            .Include(a => a.Course)
            .Where(a => !a.IsArchived);

        if (courseId.HasValue)
        {
            query = query.Where(a => a.CourseId == courseId.Value);
        }

        var assessments = await query.ToListAsync(ct);
        return assessments.Select(MapToDto).ToList();
    }

    public async Task<AssessmentDto?> GetAssessmentByIdAsync(int id, CancellationToken ct)
    {
        var assessment = await _dbContext.Assessments
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsArchived, ct);

        return assessment == null ? null : MapToDto(assessment);
    }

    public async Task<AssessmentDto> CreateAssessmentAsync(CreateAssessmentDto request, string userId, CancellationToken ct)
    {
        if (request.TotalMarks <= 0) throw new ArgumentException("Total marks must be greater than 0.");
        if (request.WeightPercentage < 0 || request.WeightPercentage > 100) throw new ArgumentException("Weight must be between 0 and 100.");

        var course = await _dbContext.Courses.FindAsync(new object[] { request.CourseId }, ct);
        if (course == null) throw new KeyNotFoundException("Course not found.");

        var currentWeight = await _dbContext.Assessments
            .Where(a => a.CourseId == request.CourseId && !a.IsArchived)
            .SumAsync(a => a.WeightPercentage, ct);

        var entity = new Assessment
        {
            CourseId = request.CourseId,
            Title = request.Title,
            Description = request.Description,
            AssessmentType = request.AssessmentType,
            TotalMarks = request.TotalMarks,
            WeightPercentage = request.WeightPercentage,
            AssessmentDate = request.AssessmentDate,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            IsPublished = request.IsPublished,
            CreatedBy = Guid.TryParse(userId, out var guid) ? guid : null,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Assessments.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        await _dbContext.Entry(entity).Reference(e => e.Course).LoadAsync(ct);
        return MapToDto(entity);
    }

    public async Task<AssessmentDto> UpdateAssessmentAsync(int id, UpdateAssessmentDto request, string userId, CancellationToken ct)
    {
        var entity = await _dbContext.Assessments
            .Include(a => a.Course)
            .Include(a => a.Results)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsArchived, ct);

        if (entity == null) throw new KeyNotFoundException("Assessment not found.");

        if (entity.Results.Any() && entity.TotalMarks != request.TotalMarks)
        {
            throw new InvalidOperationException("Cannot change Total Marks after students have been graded.");
        }

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.AssessmentType = request.AssessmentType;
        entity.TotalMarks = request.TotalMarks;
        entity.WeightPercentage = request.WeightPercentage;
        entity.AssessmentDate = request.AssessmentDate;
        entity.StartDate = request.StartDate;
        entity.DueDate = request.DueDate;
        entity.IsPublished = request.IsPublished;
        entity.UpdatedBy = Guid.TryParse(userId, out var guid) ? guid : null;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        
        if (entity.Results.Any())
        {
            await RecalculateCourseGradesAsync(entity.CourseId, ct);
        }

        return MapToDto(entity);
    }

    public async Task DeleteAssessmentAsync(int id, string userId, CancellationToken ct)
    {
        var entity = await _dbContext.Assessments.FindAsync(new object[] { id }, ct);
        if (entity == null) throw new KeyNotFoundException("Assessment not found.");

        entity.IsArchived = true;
        entity.IsActive = false;
        entity.UpdatedBy = Guid.TryParse(userId, out var guid) ? guid : null;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await RecalculateCourseGradesAsync(entity.CourseId, ct);
    }

    public async Task PublishAssessmentAsync(int id, string userId, CancellationToken ct)
    {
        var entity = await _dbContext.Assessments.FindAsync(new object[] { id }, ct);
        if (entity == null) throw new KeyNotFoundException("Assessment not found.");

        entity.IsPublished = true;
        entity.UpdatedBy = Guid.TryParse(userId, out var guid) ? guid : null;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UnpublishAssessmentAsync(int id, string userId, CancellationToken ct)
    {
        var entity = await _dbContext.Assessments.FindAsync(new object[] { id }, ct);
        if (entity == null) throw new KeyNotFoundException("Assessment not found.");

        entity.IsPublished = false;
        entity.UpdatedBy = Guid.TryParse(userId, out var guid) ? guid : null;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AssessmentResultDto>> GetResultsByAssessmentAsync(int assessmentId, CancellationToken ct)
    {
        var assessment = await _dbContext.Assessments.FindAsync(new object[] { assessmentId }, ct);
        if (assessment == null) return Array.Empty<AssessmentResultDto>();

        var enrollments = await _dbContext.Enrollments
            .Include(e => e.Student)
            .Where(e => e.CourseId == assessment.CourseId && e.Status != EnrollmentStatus.Rejected && e.Status != EnrollmentStatus.Cancelled && e.Status != EnrollmentStatus.Archived)
            .ToListAsync(ct);

        var existingResults = await _dbContext.AssessmentResults
            .Include(ar => ar.Student)
            .Where(ar => ar.AssessmentId == assessmentId)
            .ToDictionaryAsync(ar => ar.StudentId, ct);

        var dtos = new List<AssessmentResultDto>();
        foreach (var enrollment in enrollments)
        {
            if (existingResults.TryGetValue(enrollment.StudentId, out var result))
            {
                dtos.Add(MapToResultDto(result));
            }
            else
            {
                dtos.Add(new AssessmentResultDto
                {
                    Id = 0,
                    AssessmentId = assessmentId,
                    StudentId = enrollment.StudentId,
                    StudentName = enrollment.Student?.Name ?? string.Empty,
                    RegistrationNumber = enrollment.Student?.RegistrationNumber ?? string.Empty,
                    MarksObtained = 0,
                    Percentage = 0,
                    Grade = "N/A",
                    Status = "Pending",
                    Feedback = string.Empty
                });
            }
        }

        return dtos.OrderBy(d => d.StudentName).ToList();
    }

    public async Task<IReadOnlyList<AssessmentResultDto>> GetResultsByStudentAsync(int studentId, CancellationToken ct)
    {
        var results = await _dbContext.AssessmentResults
            .Include(ar => ar.Student)
            .Include(ar => ar.Assessment)
            .Where(ar => ar.StudentId == studentId && ar.Assessment.IsPublished && !ar.Assessment.IsArchived)
            .ToListAsync(ct);

        return results.Select(MapToResultDto).ToList();
    }

    public async Task SaveBulkMarksAsync(int assessmentId, BulkMarksEntryRequest request, string userId, CancellationToken ct)
    {
        var assessment = await _dbContext.Assessments
            .Include(a => a.Results)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && !a.IsArchived, ct);

        if (assessment == null) throw new KeyNotFoundException("Assessment not found.");

        var studentIds = request.Marks.Select(m => m.StudentId).ToList();
        var enrollments = await _dbContext.Enrollments
            .Where(e => e.CourseId == assessment.CourseId && studentIds.Contains(e.StudentId) && (e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Completed))
            .ToListAsync(ct);

        var graderGuid = Guid.TryParse(userId, out var g) ? g : (Guid?)null;
        
        using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        foreach (var markEntry in request.Marks)
        {
            if (markEntry.MarksObtained < 0 || markEntry.MarksObtained > assessment.TotalMarks)
            {
                throw new ArgumentException($"Marks for student {markEntry.StudentId} exceed total marks or are invalid.");
            }

            var enrollment = enrollments.FirstOrDefault(e => e.StudentId == markEntry.StudentId);
            if (enrollment == null) continue;

            var percentage = (markEntry.MarksObtained / assessment.TotalMarks) * 100m;
            var gradeLevel = _gradingService.CalculateLetterGrade(markEntry.MarksObtained, assessment.TotalMarks);

            var existingResult = assessment.Results.FirstOrDefault(r => r.StudentId == markEntry.StudentId);

            if (existingResult != null)
            {
                existingResult.MarksObtained = markEntry.MarksObtained;
                existingResult.Percentage = Math.Round(percentage, 2);
                existingResult.Grade = gradeLevel.ToString();
                existingResult.Feedback = markEntry.Feedback;
                existingResult.UpdatedAt = DateTime.UtcNow;
                existingResult.GradedBy = graderGuid;
            }
            else
            {
                assessment.Results.Add(new AssessmentResult
                {
                    AssessmentId = assessmentId,
                    StudentId = markEntry.StudentId,
                    EnrollmentId = enrollment.Id,
                    MarksObtained = markEntry.MarksObtained,
                    Percentage = Math.Round(percentage, 2),
                    Grade = gradeLevel.ToString(),
                    Feedback = markEntry.Feedback,
                    GradedBy = graderGuid,
                    GradedAt = DateTime.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        await RecalculateCourseGradesAsync(assessment.CourseId, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<AssessmentStatisticsDto> GetStatisticsAsync(int assessmentId, CancellationToken ct)
    {
        var assessment = await _dbContext.Assessments
            .Include(a => a.Results)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && !a.IsArchived, ct);

        if (assessment == null) throw new KeyNotFoundException("Assessment not found.");

        var activeEnrollments = await _dbContext.Enrollments
            .CountAsync(e => e.CourseId == assessment.CourseId && (e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Completed), ct);

        var gradedCount = assessment.Results.Count;
        var pendingCount = Math.Max(0, activeEnrollments - gradedCount);

        if (gradedCount == 0)
        {
            return new AssessmentStatisticsDto
            {
                TotalStudents = activeEnrollments,
                GradedCount = 0,
                PendingCount = pendingCount
            };
        }

        var average = assessment.Results.Average(r => r.Percentage);
        var highest = assessment.Results.Max(r => r.Percentage);
        var lowest = assessment.Results.Min(r => r.Percentage);
        
        var passCount = assessment.Results.Count(r => _gradingService.CalculateLetterGrade(r.MarksObtained, assessment.TotalMarks) == GradeLevel.Pass || _gradingService.CalculateLetterGrade(r.MarksObtained, assessment.TotalMarks) == GradeLevel.Distinction);
        var passRate = ((decimal)passCount / gradedCount) * 100m;

        return new AssessmentStatisticsDto
        {
            TotalStudents = activeEnrollments,
            GradedCount = gradedCount,
            PendingCount = pendingCount,
            AverageMark = Math.Round(average, 2),
            HighestMark = Math.Round(highest, 2),
            LowestMark = Math.Round(lowest, 2),
            PassRate = Math.Round(passRate, 2)
        };
    }

    private async Task RecalculateCourseGradesAsync(int courseId, CancellationToken ct)
    {
        var enrollments = await _dbContext.Enrollments
            .Include(e => e.Course)
            .ThenInclude(c => c.Assessments.Where(a => !a.IsArchived))
            .Where(e => e.CourseId == courseId && (e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Completed))
            .ToListAsync(ct);

        var allResults = await _dbContext.AssessmentResults
            .Where(r => r.Assessment.CourseId == courseId && !r.Assessment.IsArchived)
            .ToListAsync(ct);

        foreach (var enrollment in enrollments)
        {
            var studentResults = allResults.Where(r => r.EnrollmentId == enrollment.Id).ToList();
            decimal finalGrade = 0m;

            foreach (var assessment in enrollment.Course.Assessments)
            {
                var result = studentResults.FirstOrDefault(r => r.AssessmentId == assessment.Id);
                if (result != null)
                {
                    var weightedContribution = (result.Percentage * assessment.WeightPercentage) / 100m;
                    finalGrade += weightedContribution;
                }
            }

            enrollment.Grade = Math.Round(finalGrade, 2);
            enrollment.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    private static AssessmentDto MapToDto(Assessment entity)
    {
        return new AssessmentDto
        {
            Id = entity.Id,
            CourseId = entity.CourseId,
            CourseName = entity.Course?.CourseName ?? string.Empty,
            CourseCode = entity.Course?.CourseCode ?? string.Empty,
            Title = entity.Title,
            Description = entity.Description,
            AssessmentType = entity.AssessmentType.ToString(),
            TotalMarks = entity.TotalMarks,
            WeightPercentage = entity.WeightPercentage,
            AssessmentDate = entity.AssessmentDate,
            StartDate = entity.StartDate,
            DueDate = entity.DueDate,
            IsPublished = entity.IsPublished,
            IsActive = entity.IsActive
        };
    }

    private static AssessmentResultDto MapToResultDto(AssessmentResult entity)
    {
        return new AssessmentResultDto
        {
            Id = entity.Id,
            AssessmentId = entity.AssessmentId,
            StudentId = entity.StudentId,
            StudentName = entity.Student?.Name ?? string.Empty,
            RegistrationNumber = entity.Student?.RegistrationNumber ?? string.Empty,
            MarksObtained = entity.MarksObtained,
            Percentage = entity.Percentage,
            Grade = entity.Grade,
            Status = entity.Status.ToString(),
            Feedback = entity.Feedback
        };
    }

    public async Task<bool> IsAuthorizedAsync(int courseId, string userId, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin) return true;
        var course = await _dbContext.Courses.FindAsync(new object[] { courseId }, ct);
        return course != null && course.InstructorId == userId;
    }

    public async Task<bool> IsAuthorizedForAssessmentAsync(int assessmentId, string userId, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin) return true;
        var assessment = await _dbContext.Assessments.FindAsync(new object[] { assessmentId }, ct);
        if (assessment == null) return false;
        var course = await _dbContext.Courses.FindAsync(new object[] { assessment.CourseId }, ct);
        return course != null && course.InstructorId == userId;
    }
}
