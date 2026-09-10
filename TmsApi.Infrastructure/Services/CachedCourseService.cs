using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Caching;

namespace TmsApi.Infrastructure.Services;

public class CachedCourseService(
    HybridCache cache,
    ICourseRepository repo,
    ILogger<CachedCourseService> logger
) : ICachedCourseService
{
    public async Task<CourseResponseDto> GetCourseAsync(string code, CancellationToken ct)
    {
        var key = CacheKeys.Course(code);
        var dbHit = false;
        var dto = await cache.GetOrCreateAsync(
            key,
            (repo, code),
            async (state, token) =>
            {
                dbHit = true;
                logger.LogInformation("Cache MISS for {Key} fetching fromDB", key);
                var course =
                    await state.repo.GetByCodeAsync(state.code, token)
                    ?? throw new FileNotFoundException($"Course {state.code} notfound.");
                return new CourseResponseDto(
                    course.Id,
                    course.CourseCode,
                    course.CourseName,
                    course.Description,
                    course.Credits,
                    course.DepartmentId, course.Department?.Name, course.ProgramId, course.Program?.Name,
                    course.Level,
                    course.Semester,
                    course.CourseType,
                    course.PrerequisiteCourseId,
                    course.DurationHours,
                    course.Status,
                    course.IsPublished,
                    course.CreatedAt,
                    course.UpdatedAt,
                    course.CreatedBy,
                    course.UpdatedBy,
                    course.IsDeleted,
                    course.DeletedAt,
                    course.InstructorId,
                    null
                );
            },
            tags: [CacheKeys.CoursesTag],
            cancellationToken: ct
        );
        if (!dbHit)
        {
            TmsMeters.CacheMisses.Add(1, new KeyValuePair<string, object?>("key.kind", "course"));
            logger.LogInformation("Cache HIT for {Key}", key);
        }
        else
            TmsMeters.CacheHits.Add(1, new KeyValuePair<string, object?>("key.kind", "course"));

        return dto;
    }

    public async Task<List<CourseResponseDto>> GetAllCoursesAsync(CancellationToken ct)
    {
        var key = CacheKeys.CoursesAll;
        var dbHit = false;
        var list = await cache.GetOrCreateAsync(
            key,
            repo,
            async (state, token) =>
            {
                dbHit = true;
                logger.LogInformation("Cache MISS for {Key} fetching fromDB", key);
                var courses = await state.GetAllAsync(token);
                return courses
                    .Select(c => new CourseResponseDto(
                        c.Id,
                        c.CourseCode,
                        c.CourseName,
                        c.Description,
                        c.Credits,
                        c.DepartmentId, c.Department?.Name, c.ProgramId, c.Program?.Name,
                        c.Level,
                        c.Semester,
                        c.CourseType,
                        c.PrerequisiteCourseId,
                        c.DurationHours,
                        c.Status,
                        c.IsPublished,
                        c.CreatedAt,
                        c.UpdatedAt,
                        c.CreatedBy,
                        c.UpdatedBy,
                        c.IsDeleted,
                        c.DeletedAt,
                        c.InstructorId,
                        null
                    ))
                    .ToList();
            },
            tags: [CacheKeys.CoursesTag],
            cancellationToken: ct
        );
        if (!dbHit)
        {
            TmsMeters.CacheMisses.Add(1, new KeyValuePair<string, object?>("key.kind", "courses"));
            logger.LogInformation("Cache HIT for {Key}", key);
        }
        else
            TmsMeters.CacheHits.Add(1, new KeyValuePair<string, object?>("key.kind", "courses"));
        return list;
    }

    public async Task InvalidateCourseCacheAsync(CancellationToken ct)
    {
        logger.LogInformation("Invalidating cache tag {Tag}", CacheKeys.CoursesTag);
        await cache.RemoveByTagAsync(CacheKeys.CoursesTag, ct);
    }
}


