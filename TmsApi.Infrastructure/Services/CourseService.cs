using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class CourseService(
    TmsDbContext context,
    ILogger<CourseService> logger,
    ICachedCourseService cachedService
) : ICourseService
{
    private readonly List<CourseDto> courses = new();

    public async Task<Course?> FindAsync(int id, CancellationToken ct = default)
    {
        return await context.Courses.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var course = await context
            .Courses.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.CourseCode,
                c.CourseName,
                c.Description,
                c.Credits,
                c.DepartmentId,
                c.ProgramId,
                c.Level,
                c.Semester,
                c.CourseType,
                c.PrerequisiteCourseId,
                c.DurationHours,
                c.Status,
                c.IsPublished,
                c.CreatedAt,
                c.UpdatedAt,
                c.CreatedBy,
                c.UpdatedBy,
                c.IsDeleted,
                c.DeletedAt
            ))
            .FirstOrDefaultAsync(ct);
        if (course is null)
        {
            logger.LogWarning("Course {CourseId} not found", id);
            return null;
        }
        logger.LogInformation("Found course {CourseId}", id);
        return course;
    }

    public Task<CourseResponseDto?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return context
            .Courses.AsNoTracking()
            .Where(c => c.CourseCode == code)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.CourseCode,
                c.CourseName,
                c.Description,
                c.Credits,
                c.DepartmentId,
                c.ProgramId,
                c.Level,
                c.Semester,
                c.CourseType,
                c.PrerequisiteCourseId,
                c.DurationHours,
                c.Status,
                c.IsPublished,
                c.CreatedAt,
                c.UpdatedAt,
                c.CreatedBy,
                c.UpdatedBy,
                c.IsDeleted,
                c.DeletedAt
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct
    )
    {
        IQueryable<Course> query = context.Courses.AsNoTracking();

        var providerName = context.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            // In-memory provider can't translate EF.Functions.ILike; perform in-memory filtering
            var list = query.ToList();
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search;
                list = list
                    .Where(c =>
                        c.CourseName.Contains(s, StringComparison.OrdinalIgnoreCase)
                        || c.CourseCode.Contains(s, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            var totalCount = list.Count;

            var ordered = request.OrderBy switch
            {
                "CourseName" => request.Descending
                    ? list.OrderByDescending(c => c.CourseName)
                    : list.OrderBy(c => c.CourseName),
                "CourseCode" => request.Descending
                    ? list.OrderByDescending(c => c.CourseCode)
                    : list.OrderBy(c => c.CourseCode),
                _ => list.OrderBy(c => c.CourseName),
            };

            var items = ordered
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(c => new CourseResponseDto(
                    c.Id,
                    c.CourseCode,
                    c.CourseName,
                    c.Description,
                    c.Credits,
                    c.DepartmentId,
                    c.ProgramId,
                    c.Level,
                    c.Semester,
                    c.CourseType,
                    c.PrerequisiteCourseId,
                    c.DurationHours,
                    c.Status,
                    c.IsPublished,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.CreatedBy,
                    c.UpdatedBy,
                    c.IsDeleted,
                    c.DeletedAt
                ))
                .ToList();

            return new PagedResponse<CourseResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
            };
        }

        // Relational provider: use ILike for case-insensitive search
        query = query.Where(c =>
            EF.Functions.ILike(c.CourseName, $"%{request.Search}%")
            || EF.Functions.ILike(c.CourseCode, $"%{request.Search}%")
        );
        var totalCountDb = await query.CountAsync(ct);
        query = request.OrderBy switch
        {
            "CourseName" => request.Descending
                ? query.OrderByDescending(c => c.CourseName)
                : query.OrderBy(c => c.CourseName),

            "CourseCode" => request.Descending
                ? query.OrderByDescending(c => c.CourseCode)
                : query.OrderBy(c => c.CourseCode),

            _ => query.OrderBy(c => c.CourseName),
        };
        var itemsDb = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.CourseCode,
                c.CourseName,
                c.Description,
                c.Credits,
                c.DepartmentId,
                c.ProgramId,
                c.Level,
                c.Semester,
                c.CourseType,
                c.PrerequisiteCourseId,
                c.DurationHours,
                c.Status,
                c.IsPublished,
                c.CreatedAt,
                c.UpdatedAt,
                c.CreatedBy,
                c.UpdatedBy,
                c.IsDeleted,
                c.DeletedAt
            ))
            .ToListAsync(ct);
        return new PagedResponse<CourseResponseDto>
        {
            Items = itemsDb,
            TotalCount = totalCountDb,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        context.Courses.AsNoTracking().AnyAsync(c => c.CourseCode == code, ct);

    public async Task<CourseResponseDto> CreateAsync(
        CreateCourseRequest request,
        CancellationToken ct
    )
    {
        var course = new Course
        {
            CourseCode = request.CourseCode,
            CourseName = request.CourseName,
            Description = request.Description,
            Credits = request.Credits,
            DepartmentId = request.DepartmentId,
            ProgramId = request.ProgramId,
            Level = request.Level,
            Semester = request.Semester,
            CourseType = request.CourseType,
            PrerequisiteCourseId = request.PrerequisiteCourseId,
            DurationHours = request.DurationHours,
            Status = request.Status,
            IsPublished = request.IsPublished,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Created course {CourseId} ({CourseCode})", course.Id, course.CourseCode);
        await cachedService.InvalidateCourseCacheAsync(ct);
        return (await GetByIdAsync(course.Id, ct))!;
    }

    public async Task UpdateAsync(Course course, CancellationToken ct = default)
    {
        context.Courses.Update(course);
        await context.SaveChangesAsync(ct);
        await cachedService.InvalidateCourseCacheAsync(ct);
    }

    public async Task<IReadOnlyList<CourseResponseDto>> GetAllAsync()
    {
        var courses = await context
            .Courses.AsNoTracking()
            .Select(c => new CourseResponseDto(
                c.Id,
                c.CourseCode,
                c.CourseName,
                c.Description,
                c.Credits,
                c.DepartmentId,
                c.ProgramId,
                c.Level,
                c.Semester,
                c.CourseType,
                c.PrerequisiteCourseId,
                c.DurationHours,
                c.Status,
                c.IsPublished,
                c.CreatedAt,
                c.UpdatedAt,
                c.CreatedBy,
                c.UpdatedBy,
                c.IsDeleted,
                c.DeletedAt
            ))
            .ToListAsync();
        return courses;
    }

    public async Task<IReadOnlyList<CourseResponseDto>> GetTopCoursesAsync(
        CancellationToken ct = default
    )
    {
        var top = await context
            .Enrollments.GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Join(
                context.Courses,
                g => g.CourseId,
                c => c.Id,
                (g, c) => new CourseResponseDto(
                    c.Id,
                    c.CourseCode,
                    c.CourseName,
                    c.Description,
                    c.Credits,
                    c.DepartmentId,
                    c.ProgramId,
                    c.Level,
                    c.Semester,
                    c.CourseType,
                    c.PrerequisiteCourseId,
                    c.DurationHours,
                    c.Status,
                    c.IsPublished,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.CreatedBy,
                    c.UpdatedBy,
                    c.IsDeleted,
                    c.DeletedAt
                )
            )
            .ToListAsync(ct);

        return top;
    }
}

public record CourseDto(int Id, string Code, string Title, int MaxCapacity);

public record TopCourseDto(string Title, int EnrollmentCount);
