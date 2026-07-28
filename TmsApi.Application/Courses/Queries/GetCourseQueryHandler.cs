using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Courses.Queries;

public class GetCourseQueryHandler(ICourseService courseService)
    : IRequestHandler<GetCourseQuery, CourseResponseDto?>
{
    public Task<CourseResponseDto?> Handle(GetCourseQuery request, CancellationToken cancellationToken)
    {
        return courseService.GetByCodeAsync(request.Code, cancellationToken);
    }
}
