using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TmsApi.Api.Hubs;
using TmsApi.Application.DTOs.Enrollments;
using TmsApi.Application.Hubs;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using Xunit;

using TmsApi.Application.Interfaces;

namespace TmsApi.Tests;

public class EnrollmentServiceTests : IDisposable
{
    private readonly TmsDbContext _context;
    private readonly IHubContext<TmsHub, ITmsHubClient> _hubContext;
    private readonly INotificationService _notificationService;
    private readonly EnrollmentService _service;

    public EnrollmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<TmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new TmsDbContext(options);

        _hubContext = Substitute.For<IHubContext<TmsHub, ITmsHubClient>>();
        var clients = Substitute.For<IHubClients<ITmsHubClient>>();
        var clientProxy = Substitute.For<ITmsHubClient>();
        var groupProxy = Substitute.For<ITmsHubClient>();
        clients.All.Returns(clientProxy);
        clients.Group(Arg.Any<string>()).Returns(groupProxy);
        _hubContext.Clients.Returns(clients);

        _notificationService = Substitute.For<INotificationService>();

        _service = new EnrollmentService(
            _context,
            _hubContext,
            NullLogger<EnrollmentService>.Instance,
            _notificationService
        );
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task RequestEnrollmentAsync_WhenCourseAvailable_CreatesPendingEnrollment()
    {
        // Arrange
        var student = new Student
        {
            Id = 1,
            RegistrationNumber = "STU-001",
            Name = "Abebe",
            Email = "Abebe.doe@test.edu",
            GPA = 3.5m,
            IsActive = true,
        };
        var course = new Course
        {
            Id = 10,
            CourseCode = "CS-101",
            CourseName = "Intro to CS",
            Credits = 3,
            DepartmentId = Guid.NewGuid(),
            CourseType = "Core",
            Status = "Active",
            IsPublished = true,
            IsDeleted = false,
            IsEnrollmentOpen = true,
            MaxCapacity = 30,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Students.Add(student);
        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RequestEnrollmentAsync(student.Id, course.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(course.Id, result.CourseId);
        Assert.Equal(student.Id, result.StudentId);

        var saved = await _context.Enrollments.FirstOrDefaultAsync(e => e.StudentId == student.Id && e.CourseId == course.Id);
        Assert.NotNull(saved);
        Assert.Equal(EnrollmentStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task RequestEnrollmentAsync_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var student = new Student
        {
            Id = 2,
            RegistrationNumber = "STU-002",
            Name = "Jane Smith",
            Email = "jane.smith@test.edu",
            IsActive = true,
        };
        var course = new Course
        {
            Id = 20,
            CourseCode = "MATH-201",
            CourseName = "Calculus II",
            Credits = 4,
            DepartmentId = Guid.NewGuid(),
            CourseType = "Core",
            Status = "Active",
            IsPublished = true,
            IsDeleted = false,
            IsEnrollmentOpen = true,
            MaxCapacity = 25,
            CreatedAt = DateTime.UtcNow,
        };
        var existingEnrollment = new Enrollment
        {
            StudentId = student.Id,
            CourseId = course.Id,
            Status = EnrollmentStatus.Pending,
            EnrolledAt = DateTime.UtcNow,
        };

        _context.Students.Add(student);
        _context.Courses.Add(course);
        _context.Enrollments.Add(existingEnrollment);
        await _context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RequestEnrollmentAsync(student.Id, course.Id));

        Assert.Contains("active enrollment", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestEnrollmentAsync_WhenCourseAtCapacity_ThrowsInvalidOperationException()
    {
        // Arrange
        var student1 = new Student { Id = 3, RegistrationNumber = "STU-003", Name = "Alice B", IsActive = true };
        var student2 = new Student { Id = 4, RegistrationNumber = "STU-004", Name = "Bob C", IsActive = true };
        var course = new Course
        {
            Id = 30,
            CourseCode = "PHYS-101",
            CourseName = "Physics I",
            Credits = 3,
            DepartmentId = Guid.NewGuid(),
            CourseType = "Core",
            Status = "Active",
            IsPublished = true,
            IsDeleted = false,
            IsEnrollmentOpen = true,
            MaxCapacity = 1,
            CreatedAt = DateTime.UtcNow,
        };
        var activeEnrollment = new Enrollment
        {
            StudentId = student1.Id,
            CourseId = course.Id,
            Status = EnrollmentStatus.Approved,
            EnrolledAt = DateTime.UtcNow,
        };

        _context.Students.AddRange(student1, student2);
        _context.Courses.Add(course);
        _context.Enrollments.Add(activeEnrollment);
        await _context.SaveChangesAsync();

        // Act & Assert: student2 attempts to enroll in course with MaxCapacity=1
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RequestEnrollmentAsync(student2.Id, course.Id));

        Assert.Contains("capacity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveEnrollmentAsync_ValidPendingRequest_ApprovesAndRecordsReviewer()
    {
        // Arrange
        var student = new Student { Id = 5, RegistrationNumber = "STU-005", Name = "Charlie D", IsActive = true };
        var course = new Course
        {
            Id = 40,
            CourseCode = "CHEM-101",
            CourseName = "Chemistry I",
            Credits = 3,
            DepartmentId = Guid.NewGuid(),
            CourseType = "Core",
            Status = "Active",
            IsPublished = true,
            IsDeleted = false,
            IsEnrollmentOpen = true,
            MaxCapacity = 20,
            CreatedAt = DateTime.UtcNow,
        };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            CourseId = course.Id,
            Status = EnrollmentStatus.Pending,
            EnrolledAt = DateTime.UtcNow,
        };

        _context.Students.Add(student);
        _context.Courses.Add(course);
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ApproveEnrollmentAsync(enrollment.Id, "AdminUser");

        // Assert
        Assert.Equal("Approved", result.Status);
        Assert.Equal("AdminUser", result.ApprovedBy);
        Assert.NotNull(result.ApprovedDate);

        var updated = await _context.Enrollments.FindAsync(enrollment.Id);
        Assert.NotNull(updated);
        Assert.Equal(EnrollmentStatus.Approved, updated.Status);
        Assert.Equal("AdminUser", updated.ApprovedBy);
    }

    [Fact]
    public async Task RejectEnrollmentAsync_WithReason_TransitionsToRejected()
    {
        // Arrange
        var student = new Student { Id = 6, RegistrationNumber = "STU-006", Name = "Diana E", IsActive = true };
        var course = new Course
        {
            Id = 50,
            CourseCode = "BIO-101",
            CourseName = "Biology I",
            Credits = 3,
            DepartmentId = Guid.NewGuid(),
            CourseType = "Core",
            Status = "Active",
            IsPublished = true,
            IsDeleted = false,
            IsEnrollmentOpen = true,
            MaxCapacity = 20,
            CreatedAt = DateTime.UtcNow,
        };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            CourseId = course.Id,
            Status = EnrollmentStatus.Pending,
            EnrolledAt = DateTime.UtcNow,
        };

        _context.Students.Add(student);
        _context.Courses.Add(course);
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RejectEnrollmentAsync(enrollment.Id, "Prerequisites not met", "AdminUser");

        // Assert
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("Prerequisites not met", result.RejectionReason);
        Assert.Equal("AdminUser", result.RejectedBy);

        var updated = await _context.Enrollments.FindAsync(enrollment.Id);
        Assert.NotNull(updated);
        Assert.Equal(EnrollmentStatus.Rejected, updated.Status);
        Assert.Equal("Prerequisites not met", updated.RejectionReason);
    }

    [Fact]
    public async Task CancelEnrollmentAsync_ByStudent_TransitionsToCancelled()
    {
        // Arrange
        var student = new Student { Id = 7, RegistrationNumber = "STU-007", Name = "Evan F", IsActive = true };
        var course = new Course
        {
            Id = 60,
            CourseCode = "ENG-101",
            CourseName = "English Composition",
            Credits = 3,
            DepartmentId = Guid.NewGuid(),
            CourseType = "Core",
            Status = "Active",
            IsPublished = true,
            IsDeleted = false,
            IsEnrollmentOpen = true,
            MaxCapacity = 20,
            CreatedAt = DateTime.UtcNow,
        };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            CourseId = course.Id,
            Status = EnrollmentStatus.Pending,
            EnrolledAt = DateTime.UtcNow,
        };

        _context.Students.Add(student);
        _context.Courses.Add(course);
        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CancelEnrollmentAsync(student.Id, enrollment.Id, "Student");

        // Assert
        Assert.Equal("Cancelled", result.Status);
        Assert.NotNull(result.CancellationDate);

        var updated = await _context.Enrollments.FindAsync(enrollment.Id);
        Assert.NotNull(updated);
        Assert.Equal(EnrollmentStatus.Cancelled, updated.Status);
    }

    [Fact]
    public async Task GetAvailableCoursesAsync_CalculatesCorrectAvailabilityAndCanEnroll()
    {
        // Arrange
        var student = new Student { Id = 8, RegistrationNumber = "STU-008", Name = "Fiona G", IsActive = true };
        var courseOpen = new Course
        {
            Id = 70,
            CourseCode = "CS-201",
            CourseName = "Data Structures",
            Credits = 4,
            DepartmentId = Guid.NewGuid(),
            CourseType = "Core",
            Status = "Active",
            IsPublished = true,
            IsDeleted = false,
            IsEnrollmentOpen = true,
            MaxCapacity = 10,
            CreatedAt = DateTime.UtcNow,
        };
        var courseFull = new Course
        {
            Id = 71,
            CourseCode = "CS-202",
            CourseName = "Algorithms",
            Credits = 4,
            DepartmentId = Guid.NewGuid(),
            CourseType = "Core",
            Status = "Active",
            IsPublished = true,
            IsDeleted = false,
            IsEnrollmentOpen = true,
            MaxCapacity = 1,
            CreatedAt = DateTime.UtcNow,
        };

        var otherStudent = new Student { Id = 9, RegistrationNumber = "STU-009", Name = "George H", IsActive = true };
        var filledEnrollment = new Enrollment
        {
            StudentId = otherStudent.Id,
            CourseId = courseFull.Id,
            Status = EnrollmentStatus.Approved,
            EnrolledAt = DateTime.UtcNow,
        };

        _context.Students.AddRange(student, otherStudent);
        _context.Courses.AddRange(courseOpen, courseFull);
        _context.Enrollments.Add(filledEnrollment);
        await _context.SaveChangesAsync();

        // Act
        var courses = await _service.GetAvailableCoursesAsync(student.Id);

        // Assert
        var openDto = courses.First(c => c.Id == courseOpen.Id);
        Assert.True(openDto.CanEnroll);
        Assert.Equal(10, openDto.AvailableSeats);
        Assert.Equal("Available", openDto.AvailabilityStatus);

        var fullDto = courses.First(c => c.Id == courseFull.Id);
        Assert.False(fullDto.CanEnroll);
        Assert.Equal(0, fullDto.AvailableSeats);
        Assert.Equal("Full", fullDto.AvailabilityStatus);
    }
}
