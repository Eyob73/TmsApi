using System;

namespace TmsApi.Application.DTOs.Enrollments;

public record EnrollmentListItemDto(
    int Id,
    int StudentId,
    string StudentName,
    string StudentRegistrationNumber,
    int CourseId,
    string CourseCode,
    string CourseName,
    string Status,
    DateTime EnrollmentDate,
    string? ReviewedBy,
    DateTime? ReviewedDate,
    string? RejectionReason,
    bool IsArchived,
    decimal? Grade
);
