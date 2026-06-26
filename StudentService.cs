using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi;

public interface IStudentService
{
    Task<StudentDto?> GetByIdAsync(string id);
    Task<IReadOnlyList<StudentDto>> GetAllAsync();
    Task<IReadOnlyList<StudentDto>> GetByNameAsync(int page = 1, CancellationToken ct = default);
    Task<Student> CreateAsync(Student student, CancellationToken cancellationToken = default);
    Task<Student> UpdateAsync(Student student, CancellationToken cancellationToken = default);
}

public class StudentService : IStudentService
{
    private readonly ILogger<StudentService> _logger;
    private readonly TmsDbContext _context;

    private readonly List<StudentDto> _students = new List<StudentDto>();

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

    public async Task<IReadOnlyList<StudentDto>> GetAllAsync()
    {
        return await _context
            .Students.Select(s => new StudentDto(
                s.Id,
                s.RegistrationNumber,
                s.Name,
                s.GPA,
                s.IsActive,
                EF.Property<DateTime>(s, "LastUpdated")
            ))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<StudentDto>> GetByNameAsync(
        int page = 1,
        CancellationToken ct = default
    )
    {
        int pageSize = 20;
        if (page < 1)
            page = 1;
        var students = await _context
            .Students.OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentDto(
                s.Id,
                s.RegistrationNumber,
                s.Name,
                s.GPA,
                s.IsActive,
                EF.Property<DateTime>(s, "LastUpdated")
            ))
            .ToListAsync(ct);

        return students;
    }

    public async Task<Student> CreateAsync(
        Student student,
        CancellationToken cancellationToken = default
    )
    {
        _context.Students.Add(student);

        _context.Entry(student).Property("LastUpdated").CurrentValue = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return student;
    }

    public async Task<Student> UpdateAsync(
        Student student,
        CancellationToken cancellationToken = default
    )
    {
        _context.Students.Update(student);

        _context.Entry(student).Property("LastUpdated").CurrentValue = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return student;
    }
}

public record StudentDto(
    int Id,
    string RegistrationNumber,
    string Name,
    decimal GPA,
    bool IsActive,
    DateTime LastUpdated
);
