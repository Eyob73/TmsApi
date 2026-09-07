using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs.Users;

public record CreateUserRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters.")]
    public string UserName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string LastName { get; init; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format.")]
    public string? PhoneNumber { get; init; }

    public string? Department { get; init; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
    public string Password { get; init; } = string.Empty;

    public IReadOnlyList<string>? Roles { get; init; }

    public bool IsActive { get; init; } = true;

    public CreateUserRequest() { }

    public CreateUserRequest(
        string userName,
        string email,
        string firstName,
        string lastName,
        string? phoneNumber,
        string? department,
        string password,
        IReadOnlyList<string>? roles = null,
        bool isActive = true
    )
    {
        UserName = userName;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Department = department;
        Password = password;
        Roles = roles;
        IsActive = isActive;
    }
}
