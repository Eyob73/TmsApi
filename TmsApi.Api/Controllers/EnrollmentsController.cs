using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.DTOs.Enrollments;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;

/// <summary>
/// RESTful Enrollment Management Controller supporting full student and administrative lifecycles.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Enrollments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class EnrollmentsController : ControllerBase
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly ICourseService _courseService;
    private readonly ILogger<EnrollmentsController> _logger;

    public EnrollmentsController(
        IEnrollmentService enrollmentService,
        ICourseService courseService,
        ILogger<EnrollmentsController> logger
    )
    {
        _enrollmentService = enrollmentService;
        _courseService = courseService;
        _logger = logger;
    }

    // =========================================================================
    // Student Endpoints
    // =========================================================================

    /// <summary>
    /// Gets courses available for enrollment for the authenticated student.
    /// </summary>
    [HttpGet("available-courses")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<AvailableCourseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAvailableCourses(CancellationToken ct)
    {
        var studentId = await ResolveCurrentStudentIdAsync(ct);
        var courses = await _enrollmentService.GetAvailableCoursesAsync(studentId, ct);
        return Ok(courses);
    }

    /// <summary>
    /// Submits an enrollment request for the authenticated student.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestEnrollment(
        [FromBody] CreateEnrollmentRequest request,
        CancellationToken ct
    )
    {
        if (request.CourseId <= 0)
            return BadRequest(new ProblemDetails { Title = "Invalid Course", Detail = "A valid Course ID is required." });

        var studentId = await ResolveCurrentStudentIdAsync(ct);

        try
        {
            var enrollment = await _enrollmentService.RequestEnrollmentAsync(studentId, request.CourseId, ct);
            return CreatedAtAction(nameof(GetMyEnrollmentDetails), new { id = enrollment.Id }, enrollment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Course Not Found", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Enrollment Conflict", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    /// <summary>
    /// Gets all enrollments for the authenticated student.
    /// </summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyEnrollments(CancellationToken ct)
    {
        var studentId = await ResolveCurrentStudentIdAsync(ct);
        var enrollments = await _enrollmentService.GetMyEnrollmentsAsync(studentId, ct);
        return Ok(enrollments);
    }

    /// <summary>
    /// Gets details of an enrollment owned by the authenticated student.
    /// </summary>
    [HttpGet("my/{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(EnrollmentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyEnrollmentDetails(int id, CancellationToken ct)
    {
        var studentId = await ResolveCurrentStudentIdAsync(ct);
        var details = await _enrollmentService.GetMyEnrollmentByIdAsync(studentId, id, ct);
        if (details == null)
            return NotFound(new ProblemDetails { Title = "Enrollment Not Found", Detail = $"No enrollment #{id} found for current student." });

        return Ok(details);
    }

    /// <summary>
    /// Cancels an enrollment request for the authenticated student or an admin.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [HttpPatch("{id:int}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelEnrollment(int id, CancellationToken ct)
    {
        var studentId = await ResolveCurrentStudentIdAsync(ct);
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "User";
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Registrar");

        try
        {
            var enrollment = await _enrollmentService.CancelEnrollmentAsync(
                studentId,
                id,
                isAdmin ? "Admin" : userEmail,
                ct
            );
            return Ok(enrollment);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Enrollment Not Found", Detail = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot Cancel Enrollment", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    // =========================================================================
    // Admin / Registrar Endpoints
    // =========================================================================

    /// <summary>
    /// Gets all enrollments with pagination, filtering, searching, and sorting.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Registrar,Super Admin,Instructor,Teacher")]
    [ProducesResponseType(typeof(PagedResponse<EnrollmentListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllEnrollments(
        [FromQuery] EnrollmentFilterRequest filter,
        CancellationToken ct
    )
    {
        var isInstructorOrTeacher = User.IsInRole("Instructor") || User.IsInRole("Teacher");
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Super Admin") || User.IsInRole("Registrar");

        if (!isAdmin && isInstructorOrTeacher)
        {
            if (filter.CourseId == null)
            {
                return Forbid();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var course = await _courseService.FindAsync(filter.CourseId.Value, ct);
            if (course == null || course.InstructorId != userId)
            {
                return Forbid();
            }
        }

        var result = await _enrollmentService.GetPagedEnrollmentsAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets comprehensive details of any enrollment by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Registrar,Super Admin,Instructor,Teacher")]
    [ProducesResponseType(typeof(EnrollmentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEnrollmentById(int id, CancellationToken ct)
    {
        var details = await _enrollmentService.GetEnrollmentByIdAsync(id, ct);
        if (details == null)
            return NotFound(new ProblemDetails { Title = "Enrollment Not Found", Detail = $"Enrollment #{id} does not exist." });

        var isInstructorOrTeacher = User.IsInRole("Instructor") || User.IsInRole("Teacher");
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Super Admin") || User.IsInRole("Registrar");

        if (!isAdmin && isInstructorOrTeacher)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (details.Course == null || details.Course.InstructorId != userId)
            {
                return Forbid();
            }
        }

        return Ok(details);
    }

    /// <summary>
    /// Approves a pending enrollment request with concurrency and capacity checks.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin,Registrar,Super Admin")]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveEnrollment(int id, CancellationToken ct)
    {
        var approver = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Admin";

        try
        {
            var result = await _enrollmentService.ApproveEnrollmentAsync(id, approver, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Enrollment Not Found", Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Approval Failed", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    /// <summary>
    /// Rejects a pending enrollment request with an optional reason.
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Admin,Registrar,Super Admin")]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectEnrollment(
        int id,
        [FromBody] RejectEnrollmentRequest? request,
        CancellationToken ct
    )
    {
        var reviewer = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Admin";

        try
        {
            var result = await _enrollmentService.RejectEnrollmentAsync(id, request?.Reason, reviewer, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Enrollment Not Found", Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Rejection Failed", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    /// <summary>
    /// Archives an enrollment in a finalized status.
    /// </summary>
    [HttpPost("{id:int}/archive")]
    [Authorize(Roles = "Admin,Registrar,Super Admin")]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchiveEnrollment(int id, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Admin";

        try
        {
            var result = await _enrollmentService.ArchiveEnrollmentAsync(id, user, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Enrollment Not Found", Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Archive Failed", Detail = ex.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    // =========================================================================
    // Backward Compatibility Endpoints for /api/courses/{courseId}/enrollments
    // =========================================================================

    [HttpGet("/api/courses/{courseId:int}/enrollments/all")]
    public async Task<IActionResult> LegacyGetAll(int courseId)
    {
        var enrollments = await _enrollmentService.GetAllAsync(courseId);
        return Ok(enrollments);
    }

    [HttpGet("/api/courses/{courseId:int}/enrollments/{id:int}", Name = "GetLegacyEnrollment")]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LegacyGetEnrollment(int courseId, int id, CancellationToken ct)
    {
        var enrollment = await _enrollmentService.GetByIdAsync(courseId, id, ct);
        return enrollment is not null ? Ok(enrollment) : NotFound();
    }

    [HttpGet("/api/courses/{courseId:int}/enrollments", Name = "ListLegacyCourseEnrollments")]
    [Authorize(Roles = "Admin,Registrar,Super Admin,Instructor,Teacher")]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LegacyGetEnrollments(int courseId, CancellationToken ct)
    {
        var isInstructorOrTeacher = User.IsInRole("Instructor") || User.IsInRole("Teacher");
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Super Admin") || User.IsInRole("Registrar");

        if (!isAdmin && isInstructorOrTeacher)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var checkCourse = await _courseService.FindAsync(courseId, ct);
            if (checkCourse == null || checkCourse.InstructorId != userId)
            {
                return Forbid();
            }
        }

        var course = await _courseService.GetByIdAsync(courseId, ct);
        if (course is null)
            return NotFound();

        var enrollments = await _enrollmentService.GetByCourseAsync(courseId, ct);
        return Ok(enrollments);
    }

    [HttpPost("/api/courses/{courseId:int}/enrollments")]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LegacyEnrollStudent(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct
    )
    {
        var course = await _courseService.GetByIdAsync(courseId, ct);
        if (course is null)
            return NotFound();

        var enrollment = await _enrollmentService.CreateAsync(courseId, request, ct);
        return CreatedAtRoute(
            "GetLegacyEnrollment",
            new { courseId, id = enrollment.Id },
            enrollment
        );
    }

    [HttpDelete("/api/courses/{courseId:int}/enrollments/{id}")]
    public async Task<IActionResult> LegacyDelete(string id)
    {
        var deleted = await _enrollmentService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    private async Task<int> ResolveCurrentStudentIdAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var email = User.FindFirstValue(ClaimTypes.Email) ?? $"{userId}@tms.local";
        var firstName = User.FindFirstValue("FirstName") ?? "";
        var lastName = User.FindFirstValue(ClaimTypes.Surname) ?? "";

        return await _enrollmentService.GetOrCreateStudentForUserAsync(userId, email, firstName, lastName, ct);
    }
}
