using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Entities;

public interface ICourseService
{
    Task<CourseDto?> GetByIdAsync(string id);
    Task<IReadOnlyList<CourseDto>> GetAllAsync();
    Task<IReadOnlyList<TopCourseDto>> GetTopCoursesAsync(CancellationToken ct = default);
}

public class CourseService : ICourseService
{
    private readonly ILogger<CourseService> _logger;
    private readonly TmsDbContext _context;

    private readonly List<CourseDto> _courses =
    [
        new("CS101", "Introduction to Computer Science"),
        new("CS102", "Data Structures"),
        new("CS103", "Algorithms"),
        new("CS104", "Web Development"),
        new("CS105", "Database Systems"),
    ];

    public CourseService(ILogger<CourseService> logger, TmsDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public Task<CourseDto?> GetByIdAsync(string id)
    {
        var course = _courses.FirstOrDefault(c => c.Id == id);
        if (course is null)
        {
            _logger.LogWarning("Course {CourseId} not found", id);
        }
        else
        {
            _logger.LogInformation("Found course {CourseId}", course.Id);
        }
        return Task.FromResult(course);
    }

    public Task<IReadOnlyList<CourseDto>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<CourseDto>>(_courses);
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

public record CourseDto(string Id, string Name);

public record TopCourseDto(string Title, int EnrollmentCount);
