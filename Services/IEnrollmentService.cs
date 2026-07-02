using TmsApi.Entities;
using Tms.Api.Dtos;

namespace Tms.Api.Services;

public interface IEnrollmentService
{
    Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct);
    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(int id);
    Task<bool> DeleteAsync(string id);
}
