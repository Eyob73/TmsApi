using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TmsApi.Application.DTOs.Users;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using Xunit;

namespace TmsApi.Tests;

public class UserServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly TmsDbContext _context;
    private readonly UserManager<TmsUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddDataProtection();
        services.AddDbContext<TmsDbContext>(options =>
            options.UseInMemoryDatabase(dbName)
        );

        services.AddIdentityCore<TmsUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<TmsDbContext>()
        .AddDefaultTokenProviders();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<TmsDbContext>();
        _userManager = _serviceProvider.GetRequiredService<UserManager<TmsUser>>();
        _roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var logger = NullLogger<UserService>.Instance;
        _userService = new UserService(_userManager, _roleManager, _context, logger);

        // Seed default roles
        _roleManager.CreateAsync(new IdentityRole("Admin")).GetAwaiter().GetResult();
        _roleManager.CreateAsync(new IdentityRole("Instructor")).GetAwaiter().GetResult();
        _roleManager.CreateAsync(new IdentityRole("Student")).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task CreateUserAsync_ValidRequest_CreatesUserWithRoles()
    {
        var request = new CreateUserRequest
        {
            UserName = "johndoe",
            Email = "john@example.com",
            Password = "Password123!",
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "+1234567890",
            Department = "Computer Science",
            Roles = ["Instructor"],
            IsActive = true,
        };

        var result = await _userService.CreateUserAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("johndoe", result.Value.UserName);
        Assert.Equal("john@example.com", result.Value.Email);
        Assert.Equal("John", result.Value.FirstName);
        Assert.Equal("Doe", result.Value.LastName);
        Assert.Contains("Instructor", result.Value.Roles);
        Assert.True(result.Value.IsActive);

        var dbUser = await _userManager.FindByIdAsync(result.Value.Id);
        Assert.NotNull(dbUser);
        Assert.False(dbUser.IsDeleted);
        Assert.True(await _userManager.CheckPasswordAsync(dbUser, "Password123!"));
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUserName_ReturnsFailure()
    {
        var request1 = new CreateUserRequest
        {
            UserName = "duplicateuser",
            Email = "user1@example.com",
            Password = "Password123!",
            FirstName = "First",
            LastName = "Last",
        };
        var res1 = await _userService.CreateUserAsync(request1);
        Assert.True(res1.IsSuccess);

        var request2 = new CreateUserRequest
        {
            UserName = "duplicateuser",
            Email = "user2@example.com",
            Password = "Password123!",
            FirstName = "Second",
            LastName = "User",
        };
        var res2 = await _userService.CreateUserAsync(request2);

        Assert.False(res2.IsSuccess);
        Assert.Contains("already taken", res2.Error);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ReturnsFailure()
    {
        var request1 = new CreateUserRequest
        {
            UserName = "useralpha",
            Email = "shared@example.com",
            Password = "Password123!",
            FirstName = "First",
            LastName = "Last",
        };
        var res1 = await _userService.CreateUserAsync(request1);
        Assert.True(res1.IsSuccess);

        var request2 = new CreateUserRequest
        {
            UserName = "userbeta",
            Email = "shared@example.com",
            Password = "Password123!",
            FirstName = "Second",
            LastName = "User",
        };
        var res2 = await _userService.CreateUserAsync(request2);

        Assert.False(res2.IsSuccess);
        Assert.Contains("already registered", res2.Error);
    }

    [Fact]
    public async Task CreateUserAsync_InvalidRole_ReturnsFailure()
    {
        var request = new CreateUserRequest
        {
            UserName = "invalidroleuser",
            Email = "invalidrole@example.com",
            Password = "Password123!",
            FirstName = "Test",
            LastName = "User",
            Roles = ["NonExistentRole"],
        };

        var result = await _userService.CreateUserAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not exist", result.Error);
    }

    [Fact]
    public async Task GetUsersAsync_PaginationAndFiltering_WorksAccurately()
    {
        for (int i = 1; i <= 15; i++)
        {
            var user = new TmsUser
            {
                UserName = $"student{i:D2}",
                Email = $"student{i:D2}@tms.local",
                FirstName = $"StudentFirst{i}",
                LastName = $"StudentLast{i}",
                IsActive = i % 2 == 0,
                CreatedAt = DateTime.UtcNow.AddMinutes(i),
            };
            await _userManager.CreateAsync(user, "Password123!");
            await _userManager.AddToRoleAsync(user, i % 3 == 0 ? "Admin" : "Student");
        }

        // Paging test: page 1, pageSize 5
        var paged1 = await _userService.GetUsersAsync(new UserQueryParameters { Page = 1, PageSize = 5 });
        Assert.Equal(15, paged1.TotalCount);
        Assert.Equal(5, paged1.Items.Count);

        // Active status filter test
        var activeOnly = await _userService.GetUsersAsync(new UserQueryParameters { IsActive = true, PageSize = 20 });
        Assert.All(activeOnly.Items, u => Assert.True(u.IsActive));

        // Search filter test
        var searchRes = await _userService.GetUsersAsync(new UserQueryParameters { Search = "student05" });
        Assert.Single(searchRes.Items);
        Assert.Equal("student05", searchRes.Items[0].UserName);

        // Role filter test
        var adminRes = await _userService.GetUsersAsync(new UserQueryParameters { Role = "Admin", PageSize = 20 });
        Assert.All(adminRes.Items, u => Assert.Contains("Admin", u.Roles));
    }

    [Fact]
    public async Task UpdateUserAsync_ValidRequest_UpdatesFields()
    {
        var created = await _userService.CreateUserAsync(new CreateUserRequest
        {
            UserName = "origuser",
            Email = "orig@example.com",
            Password = "Password123!",
            FirstName = "Original",
            LastName = "Name",
            Department = "Math",
        });

        var updateReq = new UpdateUserRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            Department = "Physics",
            PhoneNumber = "+9876543210",
        };

        var updateRes = await _userService.UpdateUserAsync(created.Value.Id, updateReq);

        Assert.True(updateRes.IsSuccess);
        Assert.Equal("UpdatedFirst", updateRes.Value.FirstName);
        Assert.Equal("UpdatedLast", updateRes.Value.LastName);
        Assert.Equal("Physics", updateRes.Value.Department);
        Assert.Equal("+9876543210", updateRes.Value.PhoneNumber);
        Assert.NotNull(updateRes.Value.UpdatedAt);
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivatesAndRevokesRefreshTokens()
    {
        var created = await _userService.CreateUserAsync(new CreateUserRequest
        {
            UserName = "tokendeactive",
            Email = "tokendeactive@example.com",
            Password = "Password123!",
            FirstName = "Token",
            LastName = "Deactive",
        });

        // Add active refresh tokens
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = created.Value.Id,
            Token = "token-1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
        });
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = created.Value.Id,
            Token = "token-2",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
        });
        await _context.SaveChangesAsync();

        // Self-deactivation prevention check
        var selfDeactivate = await _userService.UpdateStatusAsync(created.Value.Id, false, currentUserId: created.Value.Id);
        Assert.False(selfDeactivate.IsSuccess);
        Assert.Contains("cannot deactivate their own account", selfDeactivate.Error);

        // Deactivate user as different admin
        var deactivateRes = await _userService.UpdateStatusAsync(created.Value.Id, false, currentUserId: "admin-999");
        Assert.True(deactivateRes.IsSuccess);
        Assert.False(deactivateRes.Value.IsActive);

        // Check tokens are revoked
        var tokens = await _context.RefreshTokens.Where(t => t.UserId == created.Value.Id).ToListAsync();
        Assert.All(tokens, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task UpdateUserRolesAsync_SyncsRolesAndPreventsSelfAdminRemoval()
    {
        var created = await _userService.CreateUserAsync(new CreateUserRequest
        {
            UserName = "roleadmin",
            Email = "roleadmin@example.com",
            Password = "Password123!",
            FirstName = "Role",
            LastName = "Admin",
            Roles = ["Admin", "Instructor"],
        });

        // Try removing Admin role from self
        var selfRemove = await _userService.UpdateUserRolesAsync(
            created.Value.Id,
            ["Instructor", "Student"],
            currentUserId: created.Value.Id
        );
        Assert.False(selfRemove.IsSuccess);
        Assert.Contains("cannot remove the Admin role", selfRemove.Error);

        // Update roles as different admin
        var syncRes = await _userService.UpdateUserRolesAsync(
            created.Value.Id,
            ["Instructor", "Student"],
            currentUserId: "other-admin"
        );
        Assert.True(syncRes.IsSuccess);
        Assert.Contains("Instructor", syncRes.Value);
        Assert.Contains("Student", syncRes.Value);
        Assert.DoesNotContain("Admin", syncRes.Value);
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidPassword_ChangesPasswordSuccessfully()
    {
        var created = await _userService.CreateUserAsync(new CreateUserRequest
        {
            UserName = "changepassuser",
            Email = "changepass@example.com",
            Password = "OldPassword123!",
            FirstName = "Change",
            LastName = "Pass",
        });

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!",
        };

        var result = await _userService.ChangePasswordAsync(created.Value.Id, request);
        Assert.True(result.IsSuccess);

        var user = await _userManager.FindByIdAsync(created.Value.Id);
        Assert.True(await _userManager.CheckPasswordAsync(user!, "NewPassword123!"));
        Assert.False(await _userManager.CheckPasswordAsync(user!, "OldPassword123!"));
    }

    [Fact]
    public async Task ResetPasswordAsync_ResetsPasswordAndRevokesTokens()
    {
        var created = await _userService.CreateUserAsync(new CreateUserRequest
        {
            UserName = "resetpassuser",
            Email = "resetpass@example.com",
            Password = "InitialPassword123!",
            FirstName = "Reset",
            LastName = "Pass",
        });

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = created.Value.Id,
            Token = "active-session-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
        });
        await _context.SaveChangesAsync();

        var request = new ResetPasswordRequest
        {
            NewPassword = "BrandNewPassword123!",
            ConfirmPassword = "BrandNewPassword123!",
        };

        var result = await _userService.ResetPasswordAsync(created.Value.Id, request);
        Assert.True(result.IsSuccess);

        var user = await _userManager.FindByIdAsync(created.Value.Id);
        Assert.True(await _userManager.CheckPasswordAsync(user!, "BrandNewPassword123!"));

        var token = await _context.RefreshTokens.FirstAsync(t => t.UserId == created.Value.Id);
        Assert.True(token.IsRevoked);
    }

    [Fact]
    public async Task SoftDeleteAndRestore_ProtectsSelfDeletionAndRestoresCorrectly()
    {
        var created = await _userService.CreateUserAsync(new CreateUserRequest
        {
            UserName = "deleteme",
            Email = "deleteme@example.com",
            Password = "Password123!",
            FirstName = "Delete",
            LastName = "Me",
        });

        // Prevent self-deletion
        var selfDel = await _userService.SoftDeleteUserAsync(created.Value.Id, currentUserId: created.Value.Id);
        Assert.False(selfDel.IsSuccess);
        Assert.Contains("cannot delete their own account", selfDel.Error);

        // Delete as another admin
        var delRes = await _userService.SoftDeleteUserAsync(created.Value.Id, currentUserId: "admin-different");
        Assert.True(delRes.IsSuccess);

        // Query filter should exclude deleted user from GetByIdAsync
        var notFound = await _userService.GetByIdAsync(created.Value.Id);
        Assert.Null(notFound);

        // Normal query should not find deleted user
        var paged = await _userService.GetUsersAsync(new UserQueryParameters { Search = "deleteme" });
        Assert.Empty(paged.Items);

        // Restore user
        var restoreRes = await _userService.RestoreUserAsync(created.Value.Id);
        Assert.True(restoreRes.IsSuccess);

        // User is accessible again
        var restored = await _userService.GetByIdAsync(created.Value.Id);
        Assert.NotNull(restored);
        Assert.Equal("deleteme", restored.UserName);
    }
}
