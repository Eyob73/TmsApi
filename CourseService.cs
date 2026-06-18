public interface ICourseService
{
    Task<CourseDto?> GetByIdAsync(string id);
    Task<IReadOnlyList<CourseDto>> GetAllAsync();
}


public class CourseService : ICourseService
{
    private readonly ILogger<CourseService> _logger;
    private readonly List<CourseDto> _courses =
    [
        new("CS101", "Introduction to Computer Science"),
        new("CS102", "Data Structures"),
        new("CS103", "Algorithms"),
        new("CS104", "Web Development"),
        new("CS105", "Database Systems")
    ];
    public CourseService(ILogger<CourseService> logger)
    {
        _logger = logger;
    }

    public Task<CourseDto?> GetByIdAsync(string id)
    {
        var course = _courses.FirstOrDefault(c => c.Id == id);
        if (course is null)
        {
            _logger.LogWarning("Course {CourseId} not found", id);
        }
        else
        {
            _logger.LogInformation("Found course {CourseId}", course.Id);
        }
        return Task.FromResult(course);
    }

    public Task<IReadOnlyList<CourseDto>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<CourseDto>>(_courses);
    }
}
public record CourseDto(string Id, string Name);

    