using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Courses.Queries;

public class GetCoursesQueryHandler(ICourseService courseService)
    : IRequestHandler<GetCoursesQuery, PagedResponse<CourseResponseDto>>
{
    public Task<PagedResponse<CourseResponseDto>> Handle(
        GetCoursesQuery request,
        CancellationToken cancellationToken
    )
    {
        return courseService.GetCoursesAsync(request.Paging, cancellationToken);
    }
}
