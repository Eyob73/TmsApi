using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/students")]
public class StudentsController(IStudentService StudentService) : ControllerBase
{
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var students = await StudentService.GetAllAsync();
        return Ok(students);
    }

    [HttpGet("paged/{page}")]
    public async Task<IActionResult> GetPaged(int page)
    {
        var students = await StudentService.GetByNameAsync(page);

        return Ok(students);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var student = await StudentService.GetByIdAsync(id);

        return student is not null ? Ok(student) : NotFound();
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(Student student)
    {
        var createdStudent = await StudentService.CreateAsync(student);
        return CreatedAtAction(nameof(GetById), new { id = createdStudent.Id }, createdStudent);
    }
}
