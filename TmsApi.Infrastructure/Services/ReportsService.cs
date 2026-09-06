using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public interface IReportsService
{
    Task<int> GetActiveStudentsCountAsync();
    Task<IReadOnlyList<CourseEnrollmentCount>> GetCourseEnrollmentCountsAsync();
    Task<IReadOnlyList<CourseAverageGPA>> GetCourseAverageGPAsAsync();
    Task<IReadOnlyList<Student>> GetStudentsWithoutEnrollmentsAsync();
}

public class ReportsService(TmsDbContext context) : IReportsService
{
    public async Task<int> GetActiveStudentsCountAsync()
    {
        var count = await context.Students.Where(s => s.IsActive && s.GPA >= 3.0m).CountAsync();

        return count;
    }

    public async Task<IReadOnlyList<CourseEnrollmentCount>> GetCourseEnrollmentCountsAsync()
    {
        var list = await context
            .Courses.Select(c => new { c.CourseName, EnrollmentCount = c.Enrollments.Count })
            .OrderByDescending(x => x.EnrollmentCount)
            .ToListAsync();

        return list.Select(x => new CourseEnrollmentCount(x.CourseName, x.EnrollmentCount)).ToList();
    }

    public async Task<IReadOnlyList<CourseAverageGPA>> GetCourseAverageGPAsAsync()
    {
        var list = await context
            .Enrollments.GroupBy(e => e.Course.CourseName)
            .Select(g => new { Course = g.Key, AverageGPA = g.Average(e => e.Student.GPA) })
            .ToListAsync();

        return list.Select(x => new CourseAverageGPA(x.Course, x.AverageGPA)).ToList();
    }

    public async Task<IReadOnlyList<Student>> GetStudentsWithoutEnrollmentsAsync()
    {
        var list = await context.Students.Where(s => !s.Enrollments.Any()).ToListAsync();

        return list;
    }
}

public record CourseEnrollmentCount(string Title, int EnrollmentCount);

public record CourseAverageGPA(string Course, decimal AverageGPA);
