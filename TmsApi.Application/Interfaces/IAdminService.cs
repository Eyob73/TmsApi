using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface IAdminService
{
    Task<List<StudentDto>> GetAllStudentsAsync();
    Task<int> ArchiveOldEnrollmentsAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default
    );
}
