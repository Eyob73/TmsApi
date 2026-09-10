using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssessmentsController : ControllerBase
{
    private readonly IAssessmentService _assessmentService;

    public AssessmentsController(IAssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssessmentDto>>> GetAssessments([FromQuery] int? courseId, [FromQuery] string? instructorId, CancellationToken ct)
    {
        // If the caller is an Instructor (not Admin), scope results to their own courses
        var resolvedInstructorId = instructorId;
        if (User.IsInRole("Instructor") && !User.IsInRole("Admin"))
        {
            resolvedInstructorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        var assessments = await _assessmentService.GetAssessmentsAsync(courseId, resolvedInstructorId, ct);
        return Ok(assessments);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AssessmentDto>> GetAssessment(int id, CancellationToken ct)
    {
        var assessment = await _assessmentService.GetAssessmentByIdAsync(id, ct);
        if (assessment == null) return NotFound();
        return Ok(assessment);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<ActionResult<AssessmentDto>> CreateAssessment([FromBody] CreateAssessmentDto request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        if (!await _assessmentService.IsAuthorizedAsync(request.CourseId, userId, isAdmin, ct))
        {
            return Forbid();
        }

        var assessment = await _assessmentService.CreateAssessmentAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetAssessment), new { id = assessment.Id }, assessment);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<ActionResult<AssessmentDto>> UpdateAssessment(int id, [FromBody] UpdateAssessmentDto request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        if (!await _assessmentService.IsAuthorizedForAssessmentAsync(id, userId, isAdmin, ct)) return Forbid();

        var assessment = await _assessmentService.UpdateAssessmentAsync(id, request, userId, ct);
        return Ok(assessment);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<ActionResult> DeleteAssessment(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        if (!await _assessmentService.IsAuthorizedForAssessmentAsync(id, userId, isAdmin, ct)) return Forbid();

        await _assessmentService.DeleteAssessmentAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id}/publish")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<ActionResult> PublishAssessment(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        if (!await _assessmentService.IsAuthorizedForAssessmentAsync(id, userId, isAdmin, ct)) return Forbid();

        await _assessmentService.PublishAssessmentAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id}/unpublish")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<ActionResult> UnpublishAssessment(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        if (!await _assessmentService.IsAuthorizedForAssessmentAsync(id, userId, isAdmin, ct)) return Forbid();

        await _assessmentService.UnpublishAssessmentAsync(id, userId, ct);
        return NoContent();
    }

    [HttpGet("{id}/results")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<ActionResult<IReadOnlyList<AssessmentResultDto>>> GetResults(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        if (!await _assessmentService.IsAuthorizedForAssessmentAsync(id, userId, isAdmin, ct)) return Forbid();

        var results = await _assessmentService.GetResultsByAssessmentAsync(id, ct);
        return Ok(results);
    }

    [HttpGet("student/{studentId}/results")]
    public async Task<ActionResult<IReadOnlyList<AssessmentResultDto>>> GetStudentResults(int studentId, CancellationToken ct)
    {
        var results = await _assessmentService.GetResultsByStudentAsync(studentId, ct);
        return Ok(results);
    }

    [HttpPost("{id}/results/bulk")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<ActionResult> SaveBulkMarks(int id, [FromBody] BulkMarksEntryRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        if (!await _assessmentService.IsAuthorizedForAssessmentAsync(id, userId, isAdmin, ct)) return Forbid();

        await _assessmentService.SaveBulkMarksAsync(id, request, userId, ct);
        return NoContent();
    }

    [HttpGet("{id}/statistics")]
    [Authorize(Roles = "Admin,Instructor")]
    public async Task<ActionResult<AssessmentStatisticsDto>> GetStatistics(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var isAdmin = User.IsInRole("Admin");
        if (!await _assessmentService.IsAuthorizedForAssessmentAsync(id, userId, isAdmin, ct)) return Forbid();

        var stats = await _assessmentService.GetStatisticsAsync(id, ct);
        return Ok(stats);
    }
}
