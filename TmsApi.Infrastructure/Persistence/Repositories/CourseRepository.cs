using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Repositories;

public class CourseRepository(TmsDbContext context) : ICourseRepository
{
    public async Task<IEnumerable<Course>> GetAllAsync(CancellationToken token) =>
        await context.Courses.AsNoTracking().Include(c => c.Enrollments).ToListAsync(token);

    public Task<Course?> GetByCodeAsync(string code, CancellationToken ct) =>
        context
            .Courses.AsNoTracking()
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.CourseCode == code, ct);
}
