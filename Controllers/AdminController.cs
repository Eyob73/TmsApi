using Microsoft.AspNetCore.Mvc;
using TmsApi;
using TmsApi.Entities;

[ApiController]
[Route("api/admin")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("students")]
    public async Task<IActionResult> GetAllStudents()
    {
        var students = await adminService.GetAllStudentsAsync();
        return Ok(students);
    }

    [HttpPost("enrollments/archive")]
    public async Task<IActionResult> ArchiveOldEnrollments(
        [FromQuery] DateTime cutoff,
        CancellationToken cancellationToken
    )
    {
        var count = await adminService.ArchiveOldEnrollmentsAsync(cutoff, cancellationToken);
        return Ok(new { ArchivedCount = count });
    }
}
