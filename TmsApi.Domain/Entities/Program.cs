namespace TmsApi.Domain.Entities;

public class Program
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
