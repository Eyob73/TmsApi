using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.DTOs.Users;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;

/// <summary>
/// Administrative controller providing full lifecycle management for users.
/// Restricted to administrators.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Super Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of users filtered by search keyword, active status, or role.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UserResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] UserQueryParameters parameters, CancellationToken ct)
    {
        var result = await _userService.GetUsersAsync(parameters, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets all roles available in the system for assignment.
    /// </summary>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableRoles(CancellationToken ct)
    {
        var roles = await _userService.GetAvailableRolesAsync(ct);
        return Ok(roles);
    }

    /// <summary>
    /// Gets a single user by ID. Returns 404 if not found or soft-deleted.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        if (user == null)
        {
            return NotFound(new { detail = $"User with ID '{id}' was not found." });
        }
        return Ok(user);
    }

    /// <summary>
    /// Creates a new user with optional role assignments.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.CreateUserAsync(request, ct);
        if (!result.IsSuccess)
        {
            return BadRequest(new { detail = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Updates an existing user's profile information.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.UpdateUserAsync(id, request, ct);
        if (!result.IsSuccess)
        {
            if (result.Error == "User not found.")
            {
                return NotFound(new { detail = result.Error });
            }
            return BadRequest(new { detail = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Activates or deactivates a user. Deactivation immediately revokes active sessions.
    /// Prevents self-deactivation.
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken ct
    )
    {
        var currentUserId = GetCurrentUserId();
        var result = await _userService.UpdateStatusAsync(id, request.IsActive, currentUserId, ct);
        if (!result.IsSuccess)
        {
            if (result.Error == "User not found.")
            {
                return NotFound(new { detail = result.Error });
            }
            return BadRequest(new { detail = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Replaces the assigned roles of a user. Prevents self-removal of Admin role.
    /// </summary>
    [HttpPut("{id}/roles")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoles(
        string id,
        [FromBody] UpdateUserRolesRequest request,
        CancellationToken ct
    )
    {
        var currentUserId = GetCurrentUserId();
        var result = await _userService.UpdateUserRolesAsync(id, request.Roles, currentUserId, ct);
        if (!result.IsSuccess)
        {
            if (result.Error == "User not found.")
            {
                return NotFound(new { detail = result.Error });
            }
            return BadRequest(new { detail = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Gets the list of roles assigned to a user.
    /// </summary>
    [HttpGet("{id}/roles")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRoles(string id, CancellationToken ct)
    {
        var roles = await _userService.GetUserRolesAsync(id, ct);
        return Ok(roles);
    }

    /// <summary>
    /// Changes a user's password with current password verification.
    /// </summary>
    [HttpPost("{id}/change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        string id,
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await _userService.ChangePasswordAsync(id, request, ct);
        if (!result.IsSuccess)
        {
            if (result.Error == "User not found.")
            {
                return NotFound(new { detail = result.Error });
            }
            return BadRequest(new { detail = result.Error });
        }

        return Ok(new { message = "Password changed successfully." });
    }

    /// <summary>
    /// Administratively resets a user's password and terminates all active sessions.
    /// </summary>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        string id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct
    )
    {
        var result = await _userService.ResetPasswordAsync(id, request, ct);
        if (!result.IsSuccess)
        {
            if (result.Error == "User not found.")
            {
                return NotFound(new { detail = result.Error });
            }
            return BadRequest(new { detail = result.Error });
        }

        return Ok(new { message = "Password reset successfully. Active sessions have been revoked." });
    }

    /// <summary>
    /// Soft-deletes a user and revokes active sessions. Prevents self-deletion.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(string id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _userService.SoftDeleteUserAsync(id, currentUserId, ct);
        if (!result.IsSuccess)
        {
            if (result.Error == "User not found.")
            {
                return NotFound(new { detail = result.Error });
            }
            return BadRequest(new { detail = result.Error });
        }

        return Ok(new { message = "User deleted successfully." });
    }

    /// <summary>
    /// Restores a soft-deleted user.
    /// </summary>
    [HttpPost("{id}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(string id, CancellationToken ct)
    {
        var result = await _userService.RestoreUserAsync(id, ct);
        if (!result.IsSuccess)
        {
            if (result.Error == "User not found.")
            {
                return NotFound(new { detail = result.Error });
            }
            return BadRequest(new { detail = result.Error });
        }

        return Ok(new { message = "User restored successfully." });
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
