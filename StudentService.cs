using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi;

public interface IStudentService
{
    Task<StudentDto?> GetByIdAsync(string id);
    Task<IReadOnlyList<StudentDto>> GetAllAsync();
    Task<IReadOnlyList<StudentDto>> GetByNameAsync(int page = 1, CancellationToken ct = default);

}


public class StudentService : IStudentService
{
    private readonly ILogger<StudentService> _logger;
    private readonly TmsDbContext _context;

    private readonly List<StudentDto> _students = new List<StudentDto>
    {
        new(1, "STU001", "Eyob Getachew", 3.8m, true),
        new(2, "STU002", "Abel Tesfaye", 3.6m, true),
        new(3, "STU003", "Sara Mohammed", 3.9m, true),
        new(4, "STU004", "John Smith", 3.5m, true),
        new(5, "STU005", "Helen Bekele", 3.7m, false)
    };
    public StudentService(ILogger<StudentService> logger, TmsDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public Task<StudentDto?> GetByIdAsync(string id)
    {
        var student = _students.FirstOrDefault(s => s.Id.ToString() == id);
        if (student is null)
        {
            _logger.LogWarning("Student {StudentId} not found", id);
        }
        else
        {
            _logger.LogInformation("Found student {StudentId}", student.Id);
        }
        return Task.FromResult(student);
    }

    public Task<IReadOnlyList<StudentDto>> GetAllAsync()
    {

        return Task.FromResult<IReadOnlyList<StudentDto>>(_students);

    }

    public async Task<IReadOnlyList<StudentDto>> GetByNameAsync(int page = 1, CancellationToken ct = default)
    {
        int pageSize = 20;
        if (page <1) page = 1;
        var students = await _context.Students
            .OrderBy(s =>s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentDto (s.Id, s.RegistrationNumber, s.Name, s.GPA, s.IsActive))
            .ToListAsync(ct);
        
        return students;
    }
}
public record StudentDto(int Id, string RegistrationNumber, string Name, decimal GPA, bool IsActive);