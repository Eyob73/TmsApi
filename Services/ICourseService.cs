using Tms.Api.Dtos;
using TmsApi.Entities;

namespace Tms.Api.Services;

public interface ICourseService
{
    Task<CourseResponseDto?> GetByIdAsync(int Id, CancellationToken ct);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<IReadOnlyList<CourseDto>> GetAllAsync();
    Task<IReadOnlyList<TopCourseDto>> GetTopCoursesAsync(CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct
    );
}
