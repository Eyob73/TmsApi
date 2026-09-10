using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.DTOs.Enrollments;

namespace TmsApi.Application.Interfaces;

public interface IEnrollmentService
{
    // Student operations
    Task<IReadOnlyList<AvailableCourseDto>> GetAvailableCoursesAsync(int studentId, CancellationToken ct = default);
    Task<EnrollmentResponseDto> RequestEnrollmentAsync(int studentId, int courseId, CancellationToken ct = default);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetMyEnrollmentsAsync(int studentId, CancellationToken ct = default);
    Task<EnrollmentDetailsDto?> GetMyEnrollmentByIdAsync(int studentId, int id, CancellationToken ct = default);
    Task<EnrollmentResponseDto> CancelEnrollmentAsync(int studentId, int id, string cancelledBy, CancellationToken ct = default);

    // Admin / Registrar operations
    Task<PagedResponse<EnrollmentListItemDto>> GetPagedEnrollmentsAsync(EnrollmentFilterRequest filter, CancellationToken ct = default);
    Task<EnrollmentDetailsDto?> GetEnrollmentByIdAsync(int id, CancellationToken ct = default);
    Task<EnrollmentResponseDto> ApproveEnrollmentAsync(int id, string approverId, CancellationToken ct = default);
    Task<EnrollmentResponseDto> RejectEnrollmentAsync(int id, string? reason, string reviewerId, CancellationToken ct = default);
    Task<EnrollmentResponseDto> ArchiveEnrollmentAsync(int id, string userId, CancellationToken ct = default);

    // Student identity resolver
    Task<int> GetOrCreateStudentForUserAsync(string userId, string email, string firstName, string lastName, CancellationToken ct = default);

    // Legacy / Backward compatibility methods
    Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct = default);
    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct = default);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(int id);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(CancellationToken ct = default);
    Task<bool> DeleteAsync(string id);
    Task<EnrollmentResponseDto?> GetByCourseAsync(int courseId, CancellationToken ct = default);
    Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct = default);
}
