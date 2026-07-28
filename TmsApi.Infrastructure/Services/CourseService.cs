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

    public async Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var course = await context
            .Courses.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count
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
            .Where(c => c.Code == code)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct
    )
    {
        IQueryable<Course> query = context.Courses.AsNoTracking();
        query = query.Where(c =>
            EF.Functions.ILike(c.Title, $"%{request.Search}%")
            || EF.Functions.ILike(c.Code, $"%{request.Search}%")
        );
        var totalCount = await query.CountAsync(ct);
        query = request.OrderBy switch
        {
            "Title" => request.Descending
                ? query.OrderByDescending(c => c.Title)
                : query.OrderBy(c => c.Title),

            "Code" => request.Descending
                ? query.OrderByDescending(c => c.Code)
                : query.OrderBy(c => c.Code),

            "MaxCapacity" => request.Descending
                ? query.OrderByDescending(c => c.MaxCapacity)
                : query.OrderBy(c => c.MaxCapacity),

            _ => query.OrderBy(c => c.Title),
        };
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count
            ))
            .ToListAsync(ct);
        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        context.Courses.AsNoTracking().AnyAsync(c => c.Code == code, ct);

    public async Task<CourseResponseDto> CreateAsync(
        CreateCourseRequest request,
        CancellationToken ct
    )
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity,
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Created course {CourseId} ({Code})", course.Id, course.Code);
        await cachedService.InvalidateCourseCacheAsync(ct);
        return (await GetByIdAsync(course.Id, ct))!;
    }

    public async Task<IReadOnlyList<CourseResponseDto>> GetAllAsync()
    {
        var courses = await context
            .Courses.AsNoTracking()
            .Select(c => new CourseResponseDto(c.Id, c.Code, c.Title, c.MaxCapacity, 0))
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
                (g, c) => new CourseResponseDto(c.Id, c.Code, c.Title, c.MaxCapacity, g.Count)
            )
            .ToListAsync(ct);

        return top;
    }
}

public record CourseDto(int Id, string Code, string Title, int MaxCapacity);

public record TopCourseDto(string Title, int EnrollmentCount);
