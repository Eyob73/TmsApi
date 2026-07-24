using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v2/search")]
public class SearchController : ControllerBase
{
    [HttpGet("search")]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> SearchCourses([FromQuery] string? term, CancellationToken ct)
    {
        // var results = await Mediator.Send(new SearchCoursesQuery(term), ct);
        return Ok(
            new
            {
                term,
                results = new[]
                {
                    new { Id = 1, Title = "Sample Course 1" },
                    new { Id = 2, Title = "Sample Course 2" },
                },
            }
        );
    }
}
