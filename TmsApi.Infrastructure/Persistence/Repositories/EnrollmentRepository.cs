using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;

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
            .AnyAsync(e => e.StudentId == studentId && e.Course.CourseCode == courseCode && !e.IsArchived
                && (e.Status == EnrollmentStatus.Pending || e.Status == EnrollmentStatus.Approved || e.Status == EnrollmentStatus.Completed), ct);

    public async Task<IEnumerable<Enrollment>> GetByStudentIdAsync(int studentId, CancellationToken ct) =>
        await context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.StudentId == studentId && !e.IsArchived)
            .ToListAsync(ct);
}
