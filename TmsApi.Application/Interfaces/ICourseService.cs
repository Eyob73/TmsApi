using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface ICourseService
{
    Task<Course?> FindAsync(int id, CancellationToken ct = default);
    Task<CourseResponseDto?> GetByIdAsync(int Id, CancellationToken ct);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<IReadOnlyList<CourseResponseDto>> GetAllAsync();
    Task<IReadOnlyList<CourseResponseDto>> GetTopCoursesAsync(CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct
    );
    Task<CourseResponseDto?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task UpdateAsync(Course course, CancellationToken ct = default);
}
