using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Repositories;

public class CourseRepository(TmsDbContext context) : ICourseRepository
{
    public Task<Course?> GetByCodeAsync(string code, CancellationToken ct) =>
        context.Courses
            .AsNoTracking()
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Code == code, ct);
}
