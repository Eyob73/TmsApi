using System;
using System.Collections.Generic;
using TmsApi.Domain.Enums;

namespace TmsApi.Application.DTOs;

public class AssessmentDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AssessmentType { get; set; } = string.Empty;
    public decimal TotalMarks { get; set; }
    public decimal WeightPercentage { get; set; }
    public DateTime AssessmentDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAssessmentDto
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AssessmentType AssessmentType { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal WeightPercentage { get; set; }
    public DateTime AssessmentDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsPublished { get; set; }
}

public class UpdateAssessmentDto : CreateAssessmentDto
{
}

public class AssessmentResultDto
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public decimal MarksObtained { get; set; }
    public decimal Percentage { get; set; }
    public string? Grade { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Feedback { get; set; }
}

public class BulkMarksEntryDto
{
    public int StudentId { get; set; }
    public decimal MarksObtained { get; set; }
    public string? Feedback { get; set; }
}

public class BulkMarksEntryRequest
{
    public List<BulkMarksEntryDto> Marks { get; set; } = new();
}

public class AssessmentStatisticsDto
{
    public int TotalStudents { get; set; }
    public int GradedCount { get; set; }
    public int PendingCount { get; set; }
    public decimal AverageMark { get; set; }
    public decimal HighestMark { get; set; }
    public decimal LowestMark { get; set; }
    public decimal PassRate { get; set; }
}
