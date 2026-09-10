using System;

namespace TmsApi.Application.DTOs.Enrollments;

public class EnrollmentDetailsDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EnrollmentDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? RejectedDate { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CompletionDate { get; set; }
    public bool IsArchived { get; set; }
    public decimal? Grade { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Student Information
    public StudentDetailInfoDto Student { get; set; } = null!;

    // Course Information
    public CourseDetailInfoDto Course { get; set; } = null!;
}

public class StudentDetailInfoDto
{
    public int Id { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public decimal GPA { get; set; }
    public bool IsActive { get; set; }
}

public class CourseDetailInfoDto
{
    public int Id { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Credits { get; set; }
    public string? DepartmentName { get; set; }
    public string? ProgramName { get; set; }
    public string? Level { get; set; }
    public string? Semester { get; set; }
    public string CourseType { get; set; } = string.Empty;
    public int? DurationHours { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? MaxCapacity { get; set; }
    public int EnrolledCount { get; set; }
    public int? AvailableSeats { get; set; }
    public string? InstructorId { get; set; }
}
