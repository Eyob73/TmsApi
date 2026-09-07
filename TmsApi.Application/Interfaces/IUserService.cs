using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.DTOs.Users;

namespace TmsApi.Application.Interfaces;

public interface IUserService
{
    Task<PagedResponse<UserResponseDto>> GetUsersAsync(UserQueryParameters parameters, CancellationToken ct = default);
    Task<UserResponseDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Result<UserResponseDto, string>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result<UserResponseDto, string>> UpdateUserAsync(string id, UpdateUserRequest request, CancellationToken ct = default);
    Task<Result<UserResponseDto, string>> UpdateStatusAsync(string id, bool isActive, string? currentUserId = null, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>, string>> UpdateUserRolesAsync(string id, IReadOnlyList<string> roles, string? currentUserId = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken ct = default);
    Task<Result<bool, string>> ChangePasswordAsync(string id, ChangePasswordRequest request, CancellationToken ct = default);
    Task<Result<bool, string>> ResetPasswordAsync(string id, ResetPasswordRequest request, CancellationToken ct = default);
    Task<Result<bool, string>> SoftDeleteUserAsync(string id, string? currentUserId = null, CancellationToken ct = default);
    Task<Result<bool, string>> RestoreUserAsync(string id, CancellationToken ct = default);
}
