using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface ICourseService
{
    Task<Course?> FindAsync(int id, CancellationToken ct = default);
    Task<CourseResponseDto?> GetByIdAsync(int Id, CancellationToken ct);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<CourseResponseDto> UpdateAsync(
        int id,
        UpdateCourseRequest request,
        CancellationToken ct = default
    );
    Task<CourseResponseDto> PatchAsync(
        int id,
        PatchCourseRequest request,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<CourseResponseDto>> GetAllAsync();
    Task<IReadOnlyList<CourseResponseDto>> GetTopCoursesAsync(CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct
    );
    Task<CourseResponseDto?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task UpdateAsync(Course course, CancellationToken ct = default);
    Task<CourseResponseDto> AssignInstructorAsync(int id, string instructorId, CancellationToken ct = default);
    Task<CourseResponseDto> RemoveInstructorAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CourseResponseDto>> GetCoursesByInstructorAsync(string instructorId, CancellationToken ct = default);
}
