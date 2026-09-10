using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs.Enrollments;

public class CreateEnrollmentRequest
{
    [Required]
    public int CourseId { get; set; }
}
