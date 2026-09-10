using System;

namespace TmsApi.Application.DTOs.Enrollments;

public class EnrollmentFilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int? CourseId { get; set; }
    public int? StudentId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SortBy { get; set; } = "enrolledat";
    public bool Descending { get; set; } = true;
    public bool IncludeArchived { get; set; } = false;
}
