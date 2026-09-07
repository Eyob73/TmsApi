using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs.Users;

public record ChangePasswordRequest
{
    [Required(ErrorMessage = "Current password is required.")]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "New password must be at least 12 characters.")]
    public string NewPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password confirmation is required.")]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation password do not match.")]
    public string ConfirmPassword { get; init; } = string.Empty;
}
