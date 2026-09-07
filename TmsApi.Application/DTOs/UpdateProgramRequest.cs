using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs;

public class UpdateProgramRequest
{
    [Required]
    [StringLength(150)]
    public required string Name { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PatchProgramRequest
{
    [StringLength(150)]
    public string? Name { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid? DepartmentId { get; set; }

    public bool? IsActive { get; set; }
}
