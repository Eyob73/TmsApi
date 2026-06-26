using Microsoft.AspNetCore.Mvc;

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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var course = await CourseService.GetByIdAsync(id);

        return course is not null ? Ok(course) : NotFound();
    }
}
