using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface ICourseRepository
{
    Task<IEnumerable<Course>> GetAllAsync(CancellationToken token);
    Task<Course?> GetByCodeAsync(string code, CancellationToken ct);
}
