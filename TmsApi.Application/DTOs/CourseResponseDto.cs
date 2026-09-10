namespace TmsApi.Application.DTOs;

public record CourseResponseDto(
    int Id,
    string CourseCode,
    string CourseName,
    string? Description,
    int Credits,
    Guid? DepartmentId,
    Guid? ProgramId,
    string? Level,
    string? Semester,
    string CourseType,
    Guid? PrerequisiteCourseId,
    int? DurationHours,
    string Status,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? CreatedBy,
    Guid? UpdatedBy,
    bool IsDeleted,
    DateTime? DeletedAt,
    string? InstructorId,
    string? InstructorName
);
