using System;
using TmsApi.Domain.Enums;

namespace TmsApi.Domain.Entities;

public class AssessmentResult
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public int StudentId { get; set; }
    public int EnrollmentId { get; set; }
    
    public decimal MarksObtained { get; set; }
    public decimal Percentage { get; set; }
    public string? Grade { get; set; } // \ Pass\, \Distinction\, \Fail\, etc.
    public AssessmentResultStatus Status { get; set; } = AssessmentResultStatus.Graded;
    public string? Feedback { get; set; }
    
    public Guid? GradedBy { get; set; }
    public DateTime GradedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Assessment Assessment { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public Enrollment Enrollment { get; set; } = null!;
}
