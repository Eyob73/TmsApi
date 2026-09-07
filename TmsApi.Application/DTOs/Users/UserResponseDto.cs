namespace TmsApi.Application.DTOs.Users;

public record UserResponseDto(
    string Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Department,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
