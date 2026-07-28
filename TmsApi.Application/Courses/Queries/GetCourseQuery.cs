using MediatR;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Courses.Queries;

public record GetCourseQuery(string Code) : IRequest<CourseResponseDto?>;
