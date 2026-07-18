using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly ILogger<AdminService> _logger;
    private readonly TmsDbContext _context;

    public AdminService(ILogger<AdminService> logger, TmsDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<List<StudentDto>> GetAllStudentsAsync()
    {
        var students =
            await _context
                .Students.IgnoreQueryFilters()
                .OrderBy(s => s.Id)
                .Select(s => new StudentDto(
                    s.Id,
                    s.RegistrationNumber,
                    s.Name,
                    s.GPA,
                    s.IsActive,
                    EF.Property<DateTime>(s, "LastUpdated")
                ))
                .ToListAsync()
            ?? new List<StudentDto>();

        if (students is null)
        {
            _logger.LogWarning("No Student Found");
        }
        else
            _logger.LogInformation("Found {Count} Students", students.Count);

        return students;
    }

    public async Task<int> ArchiveOldEnrollmentsAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default
    )
    {
        int count = await _context
            .Enrollments.Where(e => e.EnrolledAt < cutoff && !e.IsArchived)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), cancellationToken);

        _logger.LogInformation("Archived {Count} enrollments older than {Cutoff}", count, cutoff);
        return count;
    }
}
