using Asp.Versioning;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using TmsApi.Api;
using TmsApi.Api.Controllers;
using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Filters;
using TmsApi.Api.Middlewares;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Persistence.Configurations;
using TmsApi.Infrastructure.Persistence.Repositories;
using TmsApi.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TmsDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging()
);

builder
    .Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("X-Api-Version")
        );
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IAdminService, AdminService>();

builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly)
);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly)
);

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddControllers();

builder.Services.AddAuthorization();

builder
    .Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UseMiddleware<V1DeprecationMiddleware>();

app.MapControllers();

app.MapGet(
    "/_env",
    () =>
        Results.Ok(
            new
            {
                env = app.Environment.EnvironmentName,
                isDevelopment = app.Environment.IsDevelopment(),
            }
        )
);

app.MapGet(
    "/api/error",
    () =>
    {
        throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
    }
);

app.MapGet(
    "/api/assessments/results",
    () =>
        Results.Ok(
            new
            {
                courseCode = "CS-101",
                studentId = "S-001",
                letterGrade = "A",
            }
        )
);

app.MapGet(
    "/api/enrollments/worker-smoke",
    (EnrollmentWorker worker) =>
    {
        worker.ProcessBatch();
        return Results.Ok("processed");
    }
);

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    context.Database.Migrate();
    if (!context.Students.Any())
    {
        var students = new List<Student>
        {
            new()
            {
                RegistrationNumber = "TMS-2026-0001",
                Name = "AliceSmith",
                GPA = 3.8m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0002",
                Name = "Bob Jones",
                GPA = 2.9m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0003",
                Name = "Charlie Brown",
                GPA = 3.4m,
                IsActive = false,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0004",
                Name = "DianaPrince",
                GPA = 3.9m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0005",
                Name = "EvanWright",
                GPA = 2.5m,
                IsActive = true,
            },
        };
        context.Students.AddRange(students);
        var courses = new List<Course>
        {
            new()
            {
                Code = "CS-101",
                Title = "Introduction to Computer Science",
                MaxCapacity = 30,
            },
            new()
            {
                Code = "CS-201",
                Title = "Data Structures and Algorithms",
                MaxCapacity = 25,
            },
            new()
            {
                Code = "MAT-101",
                Title = "Calculus I",
                MaxCapacity = 40,
            },
        };
        context.Courses.AddRange(courses);
        context.SaveChanges();
        var enrollments = new List<Enrollment>
        {
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[0].Id,
                Grade = 4.0m,
            },
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[1].Id,
                Grade = 3.6m,
            },
            new()
            {
                StudentId = students[1].Id,
                CourseId = courses[0].Id,
                Grade = 2.8m,
            },
            new()
            {
                StudentId = students[3].Id,
                CourseId = courses[1].Id,
                Grade = 3.9m,
            },
        };
        context.Enrollments.AddRange(enrollments);
        context.SaveChanges();
    }
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    var cancellationToken = new CancellationToken();

    var students = await db
        .Students.AsNoTracking()
        .Include(s => s.Enrollments)
        .ToListAsync(cancellationToken);
    foreach (var s in students)
        Console.WriteLine($"{s.Name}: {s.Enrollments.Count} enrollments");
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    var students = await db
        .Students.Select(s => new
        {
            s.Id,
            s.Name,
            s.RegistrationNumber,
        })
        .ToListAsync();

    if (students is null)
    {
        app.Logger.LogWarning("No Student Found");
    }
    else
        app.Logger.LogInformation("Found {Count} Students", students.Count);
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    await DataSeeder.SeedAsync(context);
}

app.Run();
