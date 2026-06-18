public interface IStudentService
{
    Task<StudentDto?> GetByIdAsync(string id);
    Task<IReadOnlyList<StudentDto>> GetAllAsync();
}


public class StudentService : IStudentService
{
    private readonly ILogger<StudentService> _logger;
    private readonly List<StudentDto> _students =
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

    public Task<StudentDto?> GetByIdAsync(string id)
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

    public Task<IReadOnlyList<StudentDto>> GetAllAsync()
    {

        return Task.FromResult<IReadOnlyList<StudentDto>>(_students);

    }
}
public record StudentDto(string Id, string Name);