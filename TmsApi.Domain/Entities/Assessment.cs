using System;
using System.Collections.Generic;
using TmsApi.Domain.Enums;

namespace TmsApi.Domain.Entities;

public class Assessment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public AssessmentType AssessmentType { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal WeightPercentage { get; set; }
    
    public DateTime AssessmentDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    
    public bool IsPublished { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; } = false;
    
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Course Course { get; set; } = null!;
    public ICollection<AssessmentResult> Results { get; set; } = new List<AssessmentResult>();
}
