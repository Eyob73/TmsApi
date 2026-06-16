public interface ICourseService
{
    Task<Course?> GetByIdAsync(string id);
    Task<IReadOnlyList<Course>> GetAllAsync();
}


public class CourseService : ICourseService
{
    private readonly ILogger<CourseService> _logger;
    private readonly List<Course> _courses =
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

    public Task<Course?> GetByIdAsync(string id)
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

    public Task<IReadOnlyList<Course>> GetAllAsync()
    {
        return Task.FromResult<IReadOnlyList<Course>>(_courses);
    }
}
public record Course(string Id, string Name);

    