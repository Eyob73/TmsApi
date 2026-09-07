namespace TmsApi.Domain.Entities;

public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Program> Programs { get; set; } = new List<Program>();
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
