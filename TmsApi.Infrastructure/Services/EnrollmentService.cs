using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class EnrollmentService(TmsDbContext context, ILogger<EnrollmentService> logger)
    : IEnrollmentService
{
    private readonly Dictionary<string, EnrollmentRecord> _store = new();

    public async Task<EnrollmentResponseDto> CreateAsync(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct
    )
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow,
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);
        logger.LogInformation(
            "Enrolled student {StudentId} in course {CourseId} (enrollment {EnrollmentId})",
            request.StudentId,
            courseId,
            enrollment.Id
        );

        return (await GetByIdAsync(courseId, enrollment.Id, ct))!;
    }

    public Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
        context
            .Enrollments.AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(int id) =>
        await context
            .Enrollments.AsNoTracking()
            .Where(e => e.CourseId == id)
            .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
            .ToListAsync();

    public Task<bool> DeleteAsync(string id)
    {
        var removed = _store.Remove(id, out _);
        if (removed)
        {
            logger.LogInformation("Deleted enrollment {EnrollmentId}", id);
        }
        else
        {
            logger.LogWarning("Delete failed enrollment {EnrollmentId} not found", id);
        }
        return Task.FromResult(removed);
    }

    public async Task<EnrollmentResponseDto?> GetByCourseAsync(
        int courseId,
        CancellationToken ct
    ) =>
        await context
            .Enrollments.AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
            .FirstOrDefaultAsync(ct);
}

public record EnrollmentRecord(string Id, string StudentId, string CourseCode, DateTime EnrolledAt);

public record CreateEnrollmentRequest(string StudentId, string CourseCode);
