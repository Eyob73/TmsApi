using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Departments")]
public class DepartmentsController(IDepartmentService departmentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var departments = await departmentService.GetAllAsync(ct);
        return Ok(departments);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var department = await departmentService.GetByIdAsync(id, ct);
        return department is null ? NotFound() : Ok(department);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDepartment(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (await departmentService.NameExistsAsync(request.Name, ct))
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Department already exists",
                    Detail = $"A department named '{request.Name.Trim()}' already exists.",
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }

        var created = await departmentService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDepartment(
        Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await departmentService.UpdateAsync(id, request, ct);
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
                    Title = "Department update failed",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchDepartment(
        Guid id,
        [FromBody] PatchDepartmentRequest request,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await departmentService.PatchAsync(id, request, ct);
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
                    Title = "Department patch failed",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }
    }
}
