using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.DTOs.Users;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<TmsUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly TmsDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<TmsUser> userManager,
        RoleManager<IdentityRole> roleManager,
        TmsDbContext context,
        ILogger<UserService> logger
    )
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResponse<UserResponseDto>> GetUsersAsync(
        UserQueryParameters parameters,
        CancellationToken ct = default
    )
    {
        var query = _context.Users.AsNoTracking();

        // 1. Search filter across username, email, first name, last name
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.Trim().ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(search))
                || (u.Email != null && u.Email.ToLower().Contains(search))
                || u.FirstName.ToLower().Contains(search)
                || u.LastName.ToLower().Contains(search)
            );
        }

        // 2. Active status filter
        if (parameters.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == parameters.IsActive.Value);
        }

        // 3. Role filter
        if (!string.IsNullOrWhiteSpace(parameters.Role))
        {
            var roleNormalized = parameters.Role.Trim().ToUpper();
            query = query.Where(u => _context.UserRoles.Any(ur =>
                ur.UserId == u.Id
                && _context.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == roleNormalized)
            ));
        }

        // 4. Sorting
        query = parameters.OrderBy.ToLower() switch
        {
            "username" => parameters.Descending ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName),
            "email" => parameters.Descending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "firstname" => parameters.Descending ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            "lastname" => parameters.Descending ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
            _ => parameters.Descending ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
        };

        // 5. Total count before paging
        var totalCount = await query.CountAsync(ct);

        // 6. Pagination at database level
        var users = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(ct);

        // 7. Efficient batch role resolution (avoids N+1)
        var userIds = users.Select(u => u.Id).ToList();
        var userRoles = await (
            from ur in _context.UserRoles.AsNoTracking()
            join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name }
        ).ToListAsync(ct);

        var rolesByUser = userRoles
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.RoleName!).Where(name => name != null).ToList()
            );

        var items = users
            .Select(u => new UserResponseDto(
                u.Id,
                u.UserName ?? string.Empty,
                u.Email ?? string.Empty,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                u.Department,
                u.IsActive,
                rolesByUser.TryGetValue(u.Id, out var roles) ? roles : [],
                u.CreatedAt,
                u.UpdatedAt
            ))
            .ToList();

        return new PagedResponse<UserResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
        };
    }

    public async Task<UserResponseDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    public async Task<Result<UserResponseDto, string>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken ct = default
    )
    {
        var existingByName = await _userManager.FindByNameAsync(request.UserName.Trim());
        if (existingByName != null)
        {
            return Result<UserResponseDto, string>.Failure($"Username '{request.UserName}' is already taken.");
        }

        var existingByEmail = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (existingByEmail != null)
        {
            return Result<UserResponseDto, string>.Failure($"Email '{request.Email}' is already registered.");
        }

        // Validate roles if specified
        var requestedRoles = request.Roles ?? [];
        foreach (var role in requestedRoles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                return Result<UserResponseDto, string>.Failure($"Role '{role}' does not exist.");
            }
        }

        var user = new TmsUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<UserResponseDto, string>.Failure(errors);
        }

        if (requestedRoles.Count > 0)
        {
            var addRoleResult = await _userManager.AddToRolesAsync(user, requestedRoles);
            if (!addRoleResult.Succeeded)
            {
                var errors = string.Join("; ", addRoleResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to assign roles to user {UserId}: {Errors}", user.Id, errors);
            }
        }

        _logger.LogInformation("User created successfully: {UserId} ({UserName}, {Email})", user.Id, user.UserName, user.Email);

        var assignedRoles = await _userManager.GetRolesAsync(user);
        return Result<UserResponseDto, string>.Success(MapToDto(user, assignedRoles));
    }

    public async Task<Result<UserResponseDto, string>> UpdateUserAsync(
        string id,
        UpdateUserRequest request,
        CancellationToken ct = default
    )
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return Result<UserResponseDto, string>.Failure("User not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.UserName)
            && !string.Equals(user.UserName, request.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existingByName = await _userManager.FindByNameAsync(request.UserName.Trim());
            if (existingByName != null && existingByName.Id != user.Id)
            {
                return Result<UserResponseDto, string>.Failure($"Username '{request.UserName}' is already taken.");
            }
            user.UserName = request.UserName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Email)
            && !string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existingByEmail = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (existingByEmail != null && existingByEmail.Id != user.Id)
            {
                return Result<UserResponseDto, string>.Failure($"Email '{request.Email}' is already registered.");
            }
            user.Email = request.Email.Trim();
        }

        if (request.FirstName != null) user.FirstName = request.FirstName.Trim();
        if (request.LastName != null) user.LastName = request.LastName.Trim();
        if (request.PhoneNumber != null) user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (request.Department != null) user.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
            return Result<UserResponseDto, string>.Failure(errors);
        }

        _logger.LogInformation("User updated successfully: {UserId} ({UserName})", user.Id, user.UserName);

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserResponseDto, string>.Success(MapToDto(user, roles));
    }

    public async Task<Result<UserResponseDto, string>> UpdateStatusAsync(
        string id,
        bool isActive,
        string? currentUserId = null,
        CancellationToken ct = default
    )
    {
        if (!isActive && !string.IsNullOrEmpty(currentUserId) && string.Equals(id, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Result<UserResponseDto, string>.Failure("Administrators cannot deactivate their own account.");
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return Result<UserResponseDto, string>.Failure("User not found.");
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;

        // If deactivating, revoke active refresh tokens immediately to terminate existing sessions
        if (!isActive)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == id && !rt.IsRevoked)
                .ToListAsync(ct);

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
            }
            await _context.SaveChangesAsync(ct);
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<UserResponseDto, string>.Failure(errors);
        }

        _logger.LogInformation("User status updated: {UserId} is now {Status}", user.Id, isActive ? "Active" : "Inactive");

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UserResponseDto, string>.Success(MapToDto(user, roles));
    }

    public async Task<Result<IReadOnlyList<string>, string>> UpdateUserRolesAsync(
        string id,
        IReadOnlyList<string> roles,
        string? currentUserId = null,
        CancellationToken ct = default
    )
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return Result<IReadOnlyList<string>, string>.Failure("User not found.");
        }

        // Validate all requested roles
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                return Result<IReadOnlyList<string>, string>.Failure($"Role '{role}' does not exist.");
            }
        }

        // Prevent self-revocation of Admin role
        if (!string.IsNullOrEmpty(currentUserId)
            && string.Equals(id, currentUserId, StringComparison.OrdinalIgnoreCase)
            && !roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            return Result<IReadOnlyList<string>, string>.Failure("Administrators cannot remove the Admin role from their own account.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var toAdd = roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
        var toRemove = currentRoles.Except(roles, StringComparer.OrdinalIgnoreCase).ToList();

        if (toRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
                return Result<IReadOnlyList<string>, string>.Failure(errors);
            }
        }

        if (toAdd.Count > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
                return Result<IReadOnlyList<string>, string>.Failure(errors);
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User roles updated for {UserId}: added [{Added}], removed [{Removed}]",
            user.Id, string.Join(", ", toAdd), string.Join(", ", toRemove));

        var updatedRoles = await _userManager.GetRolesAsync(user);
        return Result<IReadOnlyList<string>, string>.Success(updatedRoles.ToList());
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(string id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return [];
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    public async Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken ct = default)
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .Select(r => r.Name!)
            .Where(name => name != null)
            .OrderBy(name => name)
            .ToListAsync(ct);

        if (roles.Count == 0)
        {
            return ["Admin", "Instructor", "Student"];
        }

        return roles;
    }

    public async Task<Result<bool, string>> ChangePasswordAsync(
        string id,
        ChangePasswordRequest request,
        CancellationToken ct = default
    )
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return Result<bool, string>.Failure("New password and confirmation password do not match.");
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return Result<bool, string>.Failure("User not found.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool, string>.Failure(errors);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password changed successfully for user {UserId}", user.Id);
        return Result<bool, string>.Success(true);
    }

    public async Task<Result<bool, string>> ResetPasswordAsync(
        string id,
        ResetPasswordRequest request,
        CancellationToken ct = default
    )
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return Result<bool, string>.Failure("New password and confirmation password do not match.");
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return Result<bool, string>.Failure("User not found.");
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool, string>.Failure(errors);
        }

        // Revoke all refresh tokens on password reset
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == id && !rt.IsRevoked)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password reset successfully by administrator for user {UserId}", user.Id);
        return Result<bool, string>.Success(true);
    }

    public async Task<Result<bool, string>> SoftDeleteUserAsync(
        string id,
        string? currentUserId = null,
        CancellationToken ct = default
    )
    {
        if (!string.IsNullOrEmpty(currentUserId) && string.Equals(id, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Result<bool, string>.Failure("Administrators cannot delete their own account.");
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return Result<bool, string>.Failure("User not found.");
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke active sessions
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == id && !rt.IsRevoked)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync(ct);
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool, string>.Failure(errors);
        }

        _logger.LogInformation("User soft-deleted: {UserId} ({UserName})", user.Id, user.UserName);
        return Result<bool, string>.Success(true);
    }

    public async Task<Result<bool, string>> RestoreUserAsync(
        string id,
        CancellationToken ct = default
    )
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user == null)
        {
            return Result<bool, string>.Failure("User not found.");
        }

        if (!user.IsDeleted)
        {
            return Result<bool, string>.Failure("User is not deleted.");
        }

        user.IsDeleted = false;
        user.DeletedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User restored: {UserId} ({UserName})", user.Id, user.UserName);
        return Result<bool, string>.Success(true);
    }

    private static UserResponseDto MapToDto(TmsUser user, IEnumerable<string> roles) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Department,
            user.IsActive,
            roles.ToList(),
            user.CreatedAt,
            user.UpdatedAt
        );
}
