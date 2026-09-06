namespace TmsApi.Application.DTOs;

public record CourseDetailDto
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string Title { get; init; }
    public int? MaxCapacity { get; init; }
    public int? EnrollmentCount { get; init; }
    public required IReadOnlyList<LinkDto> Links { get; init; }
}

public record CourseDto(int Id, string Code, string Title, int MaxCapacity, int EnrollmentCount);

public static class CourseDtoFields
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(CourseDto.Id),
        nameof(CourseDto.Code),
        nameof(CourseDto.Title),
        nameof(CourseDto.MaxCapacity),
        nameof(CourseDto.EnrollmentCount),
    };
}
