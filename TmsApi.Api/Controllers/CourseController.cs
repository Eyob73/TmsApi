using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Api.Controllers;

[Authorize(Roles = "Instructor,Admin")]
[ApiController]
[Route("api/[controller]")]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class CoursesController : ControllerBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICourseService CourseService;
    private readonly LinkGenerator linkGenerator;

    public CoursesController(
        ICourseService CourseService,
        LinkGenerator linkGenerator,
        IAuthorizationService authorizationService
    )
    {
        this.CourseService = CourseService;
        this.linkGenerator = linkGenerator;
        _authorizationService = authorizationService;
    }

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
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a course by ID")]
    [EndpointDescription(
        "Returns course details with HATEOAS links. Returns 404 if the course does not exist."
    )]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await CourseService.GetByIdAsync(id, ct);

        if (course is null)
            return NotFound();

        var selfLink = linkGenerator.GetPathByName(
            HttpContext,
            nameof(GetCourseById),
            new { id = course.Id }
        );

        var enrollmentLink = linkGenerator.GetPathByName(
            HttpContext,
            "ListCourseEnrollments",
            new { courseId = id }
        );

        var links = new List<LinkDto>
        {
            new(selfLink!, "self", "GET"),
            new(selfLink!, "update", "PUT"),
            new(selfLink!, "delete", "DELETE"),
            new(enrollmentLink!, "enrollments", "GET"),
            new(enrollmentLink!, "enroll", "POST"),
        };

        var detailDto = new CourseDetailDto
        {
            Id = course.Id,
            Code = course.CourseCode,
            Title = course.CourseName,
            MaxCapacity = null, // Capacity info moved to separate business logic
            EnrollmentCount = null, // Enrollment count handled separately
            Links = links,
        };

        return Ok(detailDto);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CourseResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List courses with pagination")]
    [EndpointDescription(
        "Returns a paginated, optionally filtered listof TMS courses. PageSize is capped at 50."
    )]
    public async Task<IActionResult> GetCourses(
        [FromQuery] PagedRequest request,
        CancellationToken ct
    )
    {
        var result = await CourseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new course")]
    [EndpointDescription(
        "Creates a course with a unique code. Returns409 if the course code already exists."
    )]
    public async Task<IActionResult> CreateCourse(CreateCourseRequest request, CancellationToken ct)
    {
        if (await CourseService.CodeExistsAsync(request.CourseCode, ct))
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Course code already exists",
                    Detail = $"A course with code '{request.CourseCode}' is already registered.",
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }

        var result = await CourseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCourse(
        int id,
        [FromBody] UpdateCourseRequest request,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var course = await CourseService.FindAsync(id, ct);
        if (course == null)
            return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, course, "CanEditCourse");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        try
        {
            var updated = await CourseService.UpdateAsync(id, request, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Course update failed",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }
    }

    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchCourse(
        int id,
        [FromBody] PatchCourseRequest request,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var course = await CourseService.FindAsync(id, ct);
        if (course == null)
            return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, course, "CanEditCourse");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        try
        {
            var updated = await CourseService.PatchAsync(id, request, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Course patch failed",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/instructor")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignInstructor(int id, [FromBody] AssignInstructorRequest request, [FromServices] Microsoft.AspNetCore.Identity.UserManager<TmsUser> userManager, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.InstructorId);
        if (user == null) return BadRequest(new { detail = "Instructor not found." });
        if (!await userManager.IsInRoleAsync(user, "Instructor") && !await userManager.IsInRoleAsync(user, "Teacher"))
            return BadRequest(new { detail = "User must have Instructor role." });

        try
        {
            var updated = await CourseService.AssignInstructorAsync(id, request.InstructorId, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/instructor")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeInstructor(int id, [FromBody] AssignInstructorRequest request, [FromServices] Microsoft.AspNetCore.Identity.UserManager<TmsUser> userManager, CancellationToken ct)
    {
        return await AssignInstructor(id, request, userManager, ct);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}/instructor")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveInstructor(int id, CancellationToken ct)
    {
        try
        {
            var updated = await CourseService.RemoveInstructorAsync(id, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Roles = "Instructor,Teacher")]
    [HttpGet("me")]
    [ProducesResponseType(typeof(IReadOnlyList<CourseResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCourses(CancellationToken ct)
    {
        var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var courses = await CourseService.GetCoursesByInstructorAsync(userId, ct);
        return Ok(courses);
    }
}



