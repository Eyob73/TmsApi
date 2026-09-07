using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Programs")]
public class ProgramsController(IProgramService programService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var programs = await programService.GetAllAsync(ct);
        return Ok(programs);
    }

    [HttpGet("by-department/{departmentId:guid}")]
    public async Task<IActionResult> GetByDepartment(Guid departmentId, CancellationToken ct)
    {
        var programs = await programService.GetByDepartmentAsync(departmentId, ct);
        return Ok(programs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var program = await programService.GetByIdAsync(id, ct);
        return program is null ? NotFound() : Ok(program);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProgramDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProgram(
        [FromBody] CreateProgramRequest request,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (await programService.NameExistsAsync(request.Name, ct))
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Program already exists",
                    Detail = $"A program named '{request.Name.Trim()}' already exists.",
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }

        var created = await programService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProgramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProgram(
        Guid id,
        [FromBody] UpdateProgramRequest request,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await programService.UpdateAsync(id, request, ct);
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
                    Title = "Program update failed",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ProgramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchProgram(
        Guid id,
        [FromBody] PatchProgramRequest request,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await programService.PatchAsync(id, request, ct);
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
                    Title = "Program patch failed",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }
    }
}
