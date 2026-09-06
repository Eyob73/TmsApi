using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Asp.Versioning;
using HealthChecks.NpgSql;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Scalar.AspNetCore;
using TmsApi.Api;
using TmsApi.Api.Authorization;
using TmsApi.Api.Controllers;
using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Filters;
using TmsApi.Api.Hubs;
using TmsApi.Api.Middlewares;
using TmsApi.Api.RateLimiting;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Transcripts;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.ExternalServices;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Persistence.Configurations;
using TmsApi.Infrastructure.Persistence.Repositories;
using TmsApi.Infrastructure.Services;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Infrastructure.Workers;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "tms-api";

builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: ServiceName, serviceVersion: "1.0.0"))
    .WithTracing(t =>
        t.AddSource(ServiceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter()
    )
    .WithMetrics(m =>
        m.AddMeter(ServiceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter()
    );

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var (partitionKey, tier) = ApiKeyResolver.Resolve(httpContext);
        return tier switch
        {
            ApiKeyTier.Paid => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"paid:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 200,
                    TokensPerPeriod = 100,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            ),
            ApiKeyTier.Free => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"free:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 30,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            ),
            _ => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"anon:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 5,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            ),
        };
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        var retryAfter = "10";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ts))
            retryAfter = ((int)ts.TotalSeconds).ToString();
        context.HttpContext.Response.Headers.RetryAfter = retryAfter;
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "Rate limit exceeded",
                Detail = $"Too many requests. Retry after {retryAfter} seconds.",
                Status = StatusCodes.Status429TooManyRequests,
                Type = "https://tms.local/errors/rate_limit_exceeded",
            },
            ct
        );
    };
    options.AddConcurrencyLimiter(
        "transcripts",
        opt =>
        {
            opt.PermitLimit = 5; // 5 in-flight transcripts maximum
            opt.QueueLimit = 20; // queue up to 20 more
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        }
    );
    options.AddTokenBucketLimiter(
        "search",
        opt =>
        {
            opt.TokenLimit = 10;
            opt.TokensPerPeriod = 5;
            opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
            opt.QueueLimit = 2;
        }
    );
});

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

builder.Services.AddSingleton(
    Channel.CreateBounded<TranscriptRequest>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait }
    )
);

builder.Services.AddResiliencePipeline(
    "certificate-api",
    pipeline =>
    {
        pipeline
            // Outer: per-request hard timeout  protects against hangs
            .AddTimeout(TimeSpan.FromSeconds(5))
            // Middle: circuit breaker  protects against sustained outage
            .AddCircuitBreaker(
                new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15),
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>(),
                    OnOpened = args =>
                    {
                        Console.WriteLine(
                            "Circuit OPENED  stopping requests to certificate service"
                        );
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        Console.WriteLine("Circuit CLOSED  certificate service recovered");
                        return ValueTask.CompletedTask;
                    },
                }
            )
            // Inner: retry with jitter  only for transient failures
            .AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>(),
                    OnRetry = args =>
                    {
                        Console.WriteLine(
                            $"Retry #{args.AttemptNumber} after {args.RetryDelay.TotalMilliseconds:F0}ms ({args.Outcome.Exception?.GetType().Name})"
                        );
                        return ValueTask.CompletedTask;
                    },
                }
            );
    }
);

builder.Services.AddHttpClient<ICertificateService, CertificateService>(
    (sp, client) =>
    {
        var baseUrl =
            sp.GetRequiredService<IConfiguration>().GetValue<string>("TmsApi:PublicBaseUrl")
            ?? "http://localhost:5001";
        client.BaseAddress = new Uri(baseUrl);
    }
);

builder
    .Services.AddHttpClient(
        "SmsService",
        client =>
        {
            client.BaseAddress = new Uri("https://sms.tms.internal");
        }
    )
    .AddStandardResilienceHandler();

builder
    .Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("alive"), tags: ["live"])
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("TmsDatabase")!,
        name: "postgres",
        tags: ["ready"]
    )
    // Optional: a Redis check, only if you wired Redis in Ex 3 or Ex 6.
    // .AddRedis(builder.Configuration.GetConnectionString("Redis")!, tags: ["ready"])
;

builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.JsonWriterOptions = new() { Indented = false };
});

builder.Services.AddScoped<TokenService>();
builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),
        };
    });

builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IAdminService, AdminService>();

builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

builder.Services.AddSingleton<ITranscriptStatusStore, InMemoryTranscriptStatusStore>();

builder.Services.AddHostedService<TranscriptWorker>();

builder.Services.AddSignalR();

// builder.Services.AddSignalR().AddStackExchangeRedis(
//     builder.Configuration.GetConnectionString("Redis")!,
//     options => options.Configuration.ChannelPrefix = "tms-signalr");

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

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
    };
});

// Production-only leave commented in lab
// builder.Services.AddStackExchangeRedisCache(options =>
// {
// options.Configuration = builder.Configuration.GetConnectionString("Redis");
// options.InstanceName = "tms:";
// });
// builder.Services.AddHybridCache();

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddOpenApi(
    "v1",
    options =>
    {
        options.AddDocumentTransformer(
            (document, context, cancellationToken) =>
            {
                document.Info.Title = "TMS API v1";
                document.Info.Version = "v1";
                return Task.CompletedTask;
            }
        );
    }
);
builder.Services.AddOpenApi(
    "v2",
    options =>
    {
        options.AddDocumentTransformer(
            (document, context, cancellationToken) =>
            {
                document.Info.Title = "TMS API v2";
                document.Info.Version = "v2";
                return Task.CompletedTask;
            }
        );
    }
);

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

// Load allowed origins from appsettings.Development.json
var allowedOrigins =
    builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];

// Register the CORS policy in the Dependency Injection container
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "TmsClient",
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials() // Vital for HttpOnly auth cookies in Session 2
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        }
    );
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

builder
    .Services.AddIdentityCore<TmsUser>(options =>
    {
        // Enterprise Password Policy
        options.Password.RequiredLength = 12;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        // Brute-Force Lockout Protection
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<TmsDbContext>();

builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        "CanEditCourse",
        policy => policy.Requirements.Add(new CourseInstructorRequirement())
    );

builder.Services.AddSingleton<IAuthorizationHandler, CourseInstructorHandler>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(
        "AuthLimiter",
        opt =>
        {
            opt.PermitLimit = 5;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueLimit = 0;
        }
    );
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("TmsClient");

app.MapHub<TmsHub>("/hubs/tms").RequireCors("TmsClient");

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.Use(
    async (context, next) =>
    {
        if (
            context.User.Identity?.IsAuthenticated == true
            || context.Request.Cookies.ContainsKey("tms_auth")
        )
        {
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Cookies.Append(
                "XSRF-TOKEN",
                tokens.RequestToken!,
                new CookieOptions
                {
                    HttpOnly = false, // MUST be false so Angular JavaScript can read it!
                    Secure = !builder.Environment.IsDevelopment(),
                    SameSite = SameSiteMode.Strict,
                }
            );
        }
        await next(context);
    }
);

app.Use(
    async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        var csp =
            context.Request.Path.StartsWithSegments("/scalar")
            || context.Request.Path.StartsWithSegments("/openapi")
                ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self';"
                : "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self';";

        context.Response.Headers.Append("Content-Security-Policy", csp);
        await next();
    }
);

app.UseMiddleware<V1DeprecationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TMS API");
        options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
    });
}

app.MapHub<TmsHub>("/hubs/tms").RequireCors("TmsClient");

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
    var providerName = context.Database.ProviderName ?? string.Empty;
    if (!providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.Migrate();
    }
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
                CourseCode = "CS-101",
                CourseName = "Introduction to Computer Science",
                Credits = 3,
                DepartmentId = Guid.NewGuid(),
                CourseType = "Core",
                Status = "Active",
                IsPublished = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                MaxCapacity = 30,
            },
            new()
            {
                CourseCode = "CS-201",
                CourseName = "Data Structures and Algorithms",
                Credits = 3,
                DepartmentId = Guid.NewGuid(),
                CourseType = "Core",
                Status = "Active",
                IsPublished = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                MaxCapacity = 25,
            },
            new()
            {
                CourseCode = "MAT-101",
                CourseName = "Calculus I",
                Credits = 4,
                DepartmentId = Guid.NewGuid(),
                CourseType = "Core",
                Status = "Active",
                IsPublished = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
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

// --- Lab-only fake certificate service ---
var attempts = 0;
app.MapPost(
        "/fake/certificates",
        async () =>
        {
            var n = Interlocked.Increment(ref attempts);

            if (n % 7 == 0)
            {
                // Hang  simulates a downstream that accepted the request and never responded.
                await Task.Delay(TimeSpan.FromSeconds(20));
                return Results.Ok(new { Status = "issued", Attempt = n });
            }
            if (n % 3 != 0)
            {
                // Transient: 503 Service Unavailable
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            if (n % 11 == 0)
            {
                // Non-transient: 400  Polly must NOT retry this
                return Results.BadRequest(new { error = "validation_failed" });
            }
            return Results.Ok(new { Status = "issued", Attempt = n });
        }
    )
    .WithTags("lab-fixtures");

app.MapHealthChecks(
        "/health/live",
        new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") }
    )
    .DisableRateLimiting();

app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }
    )
    .DisableRateLimiting();

// using (var scope = app.Services.CreateScope())
// {
//     var service = new CryptoDemoService();
//     string hash1 = service.HashUserPassword("Password123!");
//     string hash2 = service.HashUserPassword("Password123!");
//     // hash1 and hash2 are completely different strings because of unique random salts!
//     Console.WriteLine($"Hash 1: {hash1}");
//     Console.WriteLine($"Hash 2: {hash2}");
//     // Both verify to true against the same plain text:
//     bool match1 = service.VerifyUserPassword("Password123!", hash1); // true
//     bool match2 = service.VerifyUserPassword("Password123!", hash2); // true
// }
app.Run();
