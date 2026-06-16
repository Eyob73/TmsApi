public interface IStudentService
{
    Task<Student?> GetByIdAsync(string id);
    Task<IReadOnlyList<Student>> GetAllAsync();
}


public class StudentService : IStudentService
{
    private readonly ILogger<StudentService> _logger;
    private readonly List<Student> _students =
    [
        new("STU001", "Eyob Getachew"),
        new("STU002", "Abel Tesfaye"),
        new("STU003", "Sara Mohammed"),
        new("STU004", "John Smith"),
        new("STU005", "Helen Bekele")
    ];
    public StudentService(ILogger<StudentService> logger)
    {
        _logger = logger;
    }

    //

    public Task<Student?> GetByIdAsync(string id)
    {
        var student = _students.FirstOrDefault(s => s.Id == id);
        if (student is null)
        {
            _logger.LogWarning("Student {StudentId} not found", id);
        }
        else
        {
            _logger.LogInformation("Found student {StudentId}", student.Id);
        }
        return Task.FromResult(student);
    }

    public Task<IReadOnlyList<Student>> GetAllAsync()
    {

        return Task.FromResult<IReadOnlyList<Student>>(_students);

    }
}
public record Student(string Id, string Name);