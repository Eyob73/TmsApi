using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs;

public record CreateCourseRequest
{
    [
        Required,
        MaxLength(20),
        RegularExpression(
            @"^[A-Z]{2,4}\d{3,4}$",
            ErrorMessage = "Course code must be alphanumeric (e.g., CS101, CSE1010)."
        )
    ]
    public required string CourseCode { get; init; }

    [Required, MaxLength(150)]
    public required string CourseName { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required, Range(1, 10)]
    public required int Credits { get; init; }

    public Guid? DepartmentId { get; init; }

    public Guid? ProgramId { get; init; }

    [MaxLength(30)]
    public string? Level { get; init; }

    [MaxLength(30)]
    public string? Semester { get; init; }

    [Required, MaxLength(30)]
    public required string CourseType { get; init; }

    public Guid? PrerequisiteCourseId { get; init; }

    [Range(1, 500)]
    public int? DurationHours { get; init; }

    [Required, MaxLength(20)]
    public required string Status { get; init; }

    [Required]
    public required bool IsPublished { get; init; }
}
