using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Entities;
using Tms.Api.Dtos;

namespace Tms.Api.Services;

public class CourseService : ICourseService
{
    private readonly ILogger<CourseService> _logger;
    private readonly TmsDbContext _context;

    private readonly List<CourseDto> _courses = new();

    public CourseService(ILogger<CourseService> logger, TmsDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var course = await _context.Courses
        .AsNoTracking()
        .Where(c => c.Id == id)
        .Select(c => new CourseResponseDto(
            c.Id, c.Code, c.Title, c.MaxCapacity, c.Enrollments.Count))
        .FirstOrDefaultAsync(ct);
        if (course is null)
        {
            _logger.LogWarning("Course {CourseId} not found", id);
            return null;
        }
        _logger.LogInformation("Found course {CourseId}", id);
        return course;
        
        throw new NotImplementedException();

    }

    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
       var course = new Course
       {
        Code = request.Code,
        Title = request.Title,
        MaxCapacity = request.MaxCapacity
        };
        _context.Courses.Add(course);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Created course {CourseId} ({Code})", course.Id, course.Code);
        return (await GetByIdAsync(course.Id, ct))!;
    }


    public async Task<IReadOnlyList<CourseDto>> GetAllAsync()
    {
        var courses = await _context.Courses.AsNoTracking().Select(c => new CourseDto(c.Id, c.Code, c.Title, c.MaxCapacity)).ToListAsync();
        return courses;
    }

    public async Task<IReadOnlyList<TopCourseDto>> GetTopCoursesAsync(
        CancellationToken ct = default
    )
    {
        var top = await _context
            .Enrollments.GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Join(
                _context.Courses,
                g => g.CourseId,
                c => c.Id,
                (g, c) => new TopCourseDto(c.Title, g.Count)
            )
            .ToListAsync(ct);

        return top;
    }
}

public record CourseDto(int Id, string Code, string Title, int MaxCapacity);

public record TopCourseDto(string Title, int EnrollmentCount);
