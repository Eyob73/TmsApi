using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Repositories;

public class EnrollmentRepository(TmsDbContext context) : IEnrollmentRepository
{
    public Task AddAsync(Enrollment enrollment, CancellationToken ct)
    {
        context.Enrollments.Add(enrollment);
        return context.SaveChangesAsync(ct);
    }

    public Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct) =>
        context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .AnyAsync(e => e.StudentId == studentId && e.Course.CourseCode == courseCode, ct);

    public Task<IEnumerable<Enrollment>> GetByStudentIdAsync(int studentId, CancellationToken ct) =>
        context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId)
            .ToListAsync(ct)
            .ContinueWith(t => (IEnumerable<Enrollment>)t.Result, ct);
}
