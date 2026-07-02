using TmsApi.Entities;
using Tms.Api.Dtos;

namespace Tms.Api.Services;

public interface ICourseService
{
    Task<CourseResponseDto?> GetByIdAsync(int Id, CancellationToken ct);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<IReadOnlyList<CourseDto>> GetAllAsync();
    Task<IReadOnlyList<TopCourseDto>> GetTopCoursesAsync(CancellationToken ct = default);
}
