using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IEnrollmentService
{
    Task<EnrollmentResponseDto> CreateAsync(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct
    );
    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(int id);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(CancellationToken ct);
    Task<bool> DeleteAsync(string id);
    Task<EnrollmentResponseDto?> GetByCourseAsync(int courseId, CancellationToken ct);
}
