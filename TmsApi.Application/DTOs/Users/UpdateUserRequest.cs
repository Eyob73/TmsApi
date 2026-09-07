using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs.Users;

public record UpdateUserRequest
{
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters.")]
    public string? UserName { get; init; }

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string? Email { get; init; }

    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string? FirstName { get; init; }

    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string? LastName { get; init; }

    [Phone(ErrorMessage = "Invalid phone number format.")]
    public string? PhoneNumber { get; init; }

    public string? Department { get; init; }

    public bool? IsActive { get; init; }

    public UpdateUserRequest() { }

    public UpdateUserRequest(
        string? userName = null,
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        string? phoneNumber = null,
        string? department = null,
        bool? isActive = null
    )
    {
        UserName = userName;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Department = department;
        IsActive = isActive;
    }
}
