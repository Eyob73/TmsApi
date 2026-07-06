using Microsoft.AspNetCore.Mvc;
using Tms.Api.Dtos;
using Tms.Api.Services;
using TmsApi.Entities;

[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService CourseService) : ControllerBase
{
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var courses = await CourseService.GetAllAsync();
        return Ok(courses);
    }

    [HttpGet("top")]
    public async Task<IActionResult> GetTopCourses(CancellationToken ct = default)
    {
        var topCourses = await CourseService.GetTopCoursesAsync(ct);
        return Ok(topCourses);
    }

    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await CourseService.GetByIdAsync(id, ct);
        return course is not null ? Ok(course) : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> GetCourses(
        [FromQuery] PagedRequest request,
        CancellationToken ct
    )
    {
        var result = await CourseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCourse(CreateCourseRequest request, CancellationToken ct)
    {
        if (await CourseService.CodeExistsAsync(request.Code, ct))
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Course code already exists",
                    Detail = $"A course with code '{request.Code}' is already registered.",
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }

        var result = await CourseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
    }
}
