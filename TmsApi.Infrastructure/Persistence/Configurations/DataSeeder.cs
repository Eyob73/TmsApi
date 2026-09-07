using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public static class DataSeeder
{
    private static readonly (string Name, string Code)[] Departments =
    [
        ("Computer Science", "CS"),
        ("Engineering", "ENG"),
        ("Business", "BUS"),
        ("Health Sciences", "HS"),
    ];

    private static readonly (string Department, string Name, string Code)[] Programs =
    [
        ("Computer Science", "BSc Computer Science", "BSC-CS"),
        ("Computer Science", "BSc Software Engineering", "BSC-SE"),
        ("Computer Science", "BSc Data Science", "BSC-DS"),
        ("Engineering", "BSc Electrical Engineering", "BSC-EE"),
        ("Engineering", "BSc Mechanical Engineering", "BSC-ME"),
        ("Engineering", "BSc Civil Engineering", "BSC-CE"),
        ("Business", "BBA Management", "BBA-MGT"),
        ("Business", "BBA Marketing", "BBA-MKT"),
        ("Business", "BBA Finance", "BBA-FIN"),
        ("Health Sciences", "BSc Nursing", "BSC-NUR"),
        ("Health Sciences", "BSc Public Health", "BSC-PH"),
        ("Health Sciences", "BSc Pharmacy", "BSC-PHR"),
    ];

    private static readonly (
        string CourseCode,
        string CourseName,
        string DepartmentName,
        string? ProgramName,
        int MaxCapacity
    )[] Courses =
    [
        ("CS101", "Web Development Fundamentals", "Computer Science", "BSc Computer Science", 30),
        ("CS102", "TypeScript Essentials", "Computer Science", "BSc Software Engineering", 30),
        ("CS103", "Git and Collaborative Workflows", "Computer Science", "BSc Data Science", 25),
        ("CS201", "ASP.NET Core Fundamentals", "Computer Science", "BSc Computer Science", 28),
        (
            "CS202",
            "Entity Framework Core and PostgreSQL",
            "Computer Science",
            "BSc Software Engineering",
            28
        ),
        ("CS203", "Building RESTful Web APIs", "Computer Science", "BSc Data Science", 28),
        ("ENG101", "Engineering Mathematics", "Engineering", "BSc Electrical Engineering", 24),
        ("ENG201", "Digital Systems", "Engineering", "BSc Mechanical Engineering", 26),
        ("ENG301", "Structures and Materials", "Engineering", "BSc Civil Engineering", 24),
        ("BUS101", "Principles of Management", "Business", "BBA Management", 30),
        ("BUS201", "Marketing Strategy", "Business", "BBA Marketing", 28),
        ("BUS301", "Financial Analysis", "Business", "BBA Finance", 28),
        ("HS101", "Human Anatomy", "Health Sciences", "BSc Nursing", 20),
        ("HS201", "Public Health Basics", "Health Sciences", "BSc Public Health", 22),
        ("HS301", "Pharmacology Fundamentals", "Health Sciences", "BSc Pharmacy", 18),
    ];

    public static async Task SeedAsync(TmsDbContext context, CancellationToken ct = default)
    {
        var providerName = context.Database.ProviderName ?? string.Empty;
        if (!providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            await context.Database.MigrateAsync(ct);
        }

        if (await context.Departments.AnyAsync(ct))
        {
            return;
        }

        var departments = Departments
            .Select(d => new Department
            {
                Id = Guid.NewGuid(),
                Name = d.Name,
                Code = d.Code,
                Description = $"{d.Name} department",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();

        context.Departments.AddRange(departments);
        await context.SaveChangesAsync(ct);

        var departmentLookup = departments.ToDictionary(
            d => d.Name,
            d => d.Id,
            StringComparer.OrdinalIgnoreCase
        );

        var programs = Programs
            .Select(p => new TmsApi.Domain.Entities.Program
            {
                Id = Guid.NewGuid(),
                Name = p.Name,
                Code = p.Code,
                Description = $"{p.Name} program",
                DepartmentId = departmentLookup[p.Department],
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();

        context.Programs.AddRange(programs);
        await context.SaveChangesAsync(ct);

        var programLookup = programs.ToDictionary(
            p => p.Name,
            p => p.Id,
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var (courseCode, courseName, departmentName, programName, maxCapacity) in Courses)
        {
            var departmentId = departmentLookup[departmentName];
            context.Courses.Add(
                new Course
                {
                    CourseCode = courseCode,
                    CourseName = courseName,
                    Credits = 3,
                    DepartmentId = departmentId,
                    ProgramId = programName is null ? null : programLookup[programName],
                    CourseType = "Core",
                    Status = "Active",
                    IsPublished = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    MaxCapacity = maxCapacity,
                }
            );
        }

        await context.SaveChangesAsync(ct);
    }
}
