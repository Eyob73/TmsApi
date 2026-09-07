using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TmsApi.Api.Controllers;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.DTOs.Users;
using TmsApi.Application.Interfaces;
using Xunit;

namespace TmsApi.Tests;

public class UsersControllerTests
{
    private readonly IUserService _userService;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _userService = Substitute.For<IUserService>();
        _controller = new UsersController(_userService, NullLogger<UsersController>.Instance);
    }

    private void SetUserContext(string userId, string role = "Admin")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetUsers_ReturnsOkResult_WithPagedData()
    {
        var parameters = new UserQueryParameters { Page = 1, PageSize = 10 };
        var expectedResponse = new PagedResponse<UserResponseDto>
        {
            Items =
            [
                new UserResponseDto("u1", "admin", "admin@tms.local", "Admin", "User", null, null, true, ["Admin"], DateTime.UtcNow, null)
            ],
            TotalCount = 1,
            Page = 1,
            PageSize = 10
        };

        _userService.GetUsersAsync(parameters, Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var result = await _controller.GetUsers(parameters, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<PagedResponse<UserResponseDto>>(okResult.Value);
        Assert.Single(value.Items);
    }

    [Fact]
    public async Task GetById_WhenUserExists_ReturnsOk()
    {
        var user = new UserResponseDto("u1", "admin", "admin@tms.local", "Admin", "User", null, null, true, ["Admin"], DateTime.UtcNow, null);
        _userService.GetByIdAsync("u1", Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _controller.GetById("u1", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(user, okResult.Value);
    }

    [Fact]
    public async Task GetById_WhenUserNotFound_ReturnsNotFound()
    {
        _userService.GetByIdAsync("not-found", Arg.Any<CancellationToken>())
            .Returns((UserResponseDto?)null);

        var result = await _controller.GetById("not-found", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Create_WhenSuccessful_ReturnsCreatedAtAction()
    {
        var request = new CreateUserRequest
        {
            UserName = "newuser",
            Email = "new@tms.local",
            Password = "Password123!",
            FirstName = "New",
            LastName = "User",
        };
        var user = new UserResponseDto("new-id", "newuser", "new@tms.local", "New", "User", null, null, true, ["Student"], DateTime.UtcNow, null);

        _userService.CreateUserAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result<UserResponseDto, string>.Success(user));

        var result = await _controller.Create(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(UsersController.GetById), createdResult.ActionName);
        Assert.Equal("new-id", createdResult.RouteValues?["id"]);
    }

    [Fact]
    public async Task Create_WhenFailed_ReturnsBadRequest()
    {
        var request = new CreateUserRequest
        {
            UserName = "existing",
            Email = "existing@tms.local",
            Password = "Password123!",
            FirstName = "First",
            LastName = "Last",
        };

        _userService.CreateUserAsync(request, Arg.Any<CancellationToken>())
            .Returns(Result<UserResponseDto, string>.Failure("Username 'existing' is already taken."));

        var result = await _controller.Create(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task UpdateStatus_PassesCurrentUserId_AndReturnsOk()
    {
        SetUserContext("current-admin-id");
        var request = new UpdateUserStatusRequest(false);
        var updated = new UserResponseDto("target-id", "user", "user@tms.local", "User", "Test", null, null, false, ["Student"], DateTime.UtcNow, DateTime.UtcNow);

        _userService.UpdateStatusAsync("target-id", false, "current-admin-id", Arg.Any<CancellationToken>())
            .Returns(Result<UserResponseDto, string>.Success(updated));

        var result = await _controller.UpdateStatus("target-id", request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(updated, okResult.Value);
    }

    [Fact]
    public async Task SoftDelete_PassesCurrentUserId_AndReturnsOk()
    {
        SetUserContext("current-admin-id");

        _userService.SoftDeleteUserAsync("target-id", "current-admin-id", Arg.Any<CancellationToken>())
            .Returns(Result<bool, string>.Success(true));

        var result = await _controller.SoftDelete("target-id", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Restore_WhenSuccessful_ReturnsOk()
    {
        _userService.RestoreUserAsync("target-id", Arg.Any<CancellationToken>())
            .Returns(Result<bool, string>.Success(true));

        var result = await _controller.Restore("target-id", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
