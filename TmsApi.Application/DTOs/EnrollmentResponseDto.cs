using System;

namespace TmsApi.Application.DTOs;

public record EnrollmentResponseDto
{
    public int Id { get; init; }
    public int StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public int CourseId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseName { get; init; } = string.Empty;
    public string Status { get; init; } = "Pending";
    public DateTime EnrollmentDate { get; init; }
    public DateTime EnrolledAt => EnrollmentDate;
    public DateTime? ApprovedDate { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? RejectedDate { get; init; }
    public string? RejectedBy { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime? CancellationDate { get; init; }
    public string? CancelledBy { get; init; }
    public DateTime? CompletionDate { get; init; }
    public bool IsArchived { get; init; }
    public decimal? Grade { get; init; }

    public EnrollmentResponseDto() { }

    public EnrollmentResponseDto(int id, int courseId, int studentId, bool isArchived, DateTime enrolledAt)
    {
        Id = id;
        CourseId = courseId;
        StudentId = studentId;
        IsArchived = isArchived;
        EnrollmentDate = enrolledAt;
    }

    public EnrollmentResponseDto(
        int id,
        int studentId,
        string studentName,
        int courseId,
        string courseCode,
        string courseName,
        string status,
        DateTime enrollmentDate,
        DateTime? approvedDate = null,
        string? approvedBy = null,
        DateTime? rejectedDate = null,
        string? rejectedBy = null,
        string? rejectionReason = null,
        DateTime? cancellationDate = null,
        string? cancelledBy = null,
        DateTime? completionDate = null,
        bool isArchived = false,
        decimal? grade = null
    )
    {
        Id = id;
        StudentId = studentId;
        StudentName = studentName;
        CourseId = courseId;
        CourseCode = courseCode;
        CourseName = courseName;
        Status = status;
        EnrollmentDate = enrollmentDate;
        ApprovedDate = approvedDate;
        ApprovedBy = approvedBy;
        RejectedDate = rejectedDate;
        RejectedBy = rejectedBy;
        RejectionReason = rejectionReason;
        CancellationDate = cancellationDate;
        CancelledBy = cancelledBy;
        CompletionDate = completionDate;
        IsArchived = isArchived;
        Grade = grade;
    }
}
