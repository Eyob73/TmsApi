using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IEnrollmentRepository
{
    Task AddAsync(Enrollment enrollment, CancellationToken ct);
    Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct);
    Task<IEnumerable<Enrollment>> GetByStudentIdAsync(int studentId, CancellationToken ct);
}
