using System;

namespace TmsApi.Application.DTOs.Enrollments;

public record AvailableCourseDto(
    int Id,
    string CourseCode,
    string CourseName,
    string? Description,
    int Credits,
    string? DepartmentName,
    string? ProgramName,
    string? Level,
    string? Semester,
    string CourseType,
    int? DurationHours,
    string? InstructorId,
    int? MaxCapacity,
    int EnrolledCount,
    int? AvailableSeats,
    bool IsEnrollmentOpen,
    DateTime? EnrollmentStartDate,
    DateTime? EnrollmentEndDate,
    string AvailabilityStatus,
    string? StudentEnrollmentStatus,
    bool CanEnroll
);
