using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<TmsUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly TmsDbContext _context;
    private readonly TokenService _tokenService;

    public AuthController(
        UserManager<TmsUser> userManager,
        RoleManager<IdentityRole> roleManager,
        TmsDbContext context,
        TokenService tokenService
    )
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tokenService = tokenService;
    }

    public record RegisterRequest(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string Role
    );

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            // Prevent account enumeration by returning a generic response
            return Ok(new { message = "Registration request received." });
        }
        var user = new TmsUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }
        // Ensure requested role exists
        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            await _roleManager.CreateAsync(new IdentityRole(request.Role));
        }
        await _userManager.AddToRoleAsync(user, request.Role);
        return Ok(new { message = "Registration successful." });
    }

    [EnableRateLimiting("AuthLimiter")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email!);
        if (user == null)
            return Unauthorized(new { detail = "Invalid credentials." });
        if (await _userManager.IsLockedOutAsync(user))
        {
            return StatusCode(
                423,
                new { detail = "Account locked dueto multiple failed login attempts." }
            );
        }
        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { detail = "Invalid credentials." });
        }
        await _userManager.ResetAccessFailedCountAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateJwt(user, roles);
        // Issue initial Refresh Token
        var refreshToken = new RefreshToken
        {
            Token = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsUsed = false,
            IsRevoked = false,
        };
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = refreshToken.ExpiresAt
        };
        Response.Cookies.Append("refreshToken", refreshToken.Token, cookieOptions);

        return Ok(new { accessToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var requestRefreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(requestRefreshToken))
        {
            return Unauthorized(new { detail = "Refresh token is missing." });
        }

        var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt =>
            rt.Token == requestRefreshToken
        );
        if (storedToken == null)
        {
            return Unauthorized(new { detail = "Invalid refresh token." });
        }
        // Theft Detection: If an ALREADY-USED token is submitted, revoke ALL tokens for this user!
        if (storedToken.IsUsed)
        {
            var userTokens = await _context
                .RefreshTokens.Where(rt => rt.UserId == storedToken.UserId)
                .ToListAsync();
            foreach (var t in userTokens)
            {
                t.IsRevoked = true;
            }
            await _context.SaveChangesAsync();
            Response.Cookies.Delete("refreshToken", new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
            return Unauthorized(
                new { detail = "Token theft detected. All user sessions revoked." }
            );
        }
        if (storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            Response.Cookies.Delete("refreshToken", new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
            return Unauthorized(new { detail = "Refresh token expired or revoked." });
        }
        // Mark current token as used
        storedToken.IsUsed = true;
        // Issue brand-new Refresh Token pair
        var newRefreshToken = new RefreshToken
        {
            Token = Guid.NewGuid().ToString("N"),
            UserId = storedToken.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsUsed = false,
            IsRevoked = false,
        };
        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();
        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        var roles = await _userManager.GetRolesAsync(user!);
        var newAccessToken = _tokenService.GenerateJwt(user!, roles);
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = newRefreshToken.ExpiresAt
        };
        Response.Cookies.Append("refreshToken", newRefreshToken.Token, cookieOptions);
        
        return Ok(new { accessToken = newAccessToken });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });
        return Ok(new { message = "Logged out successfully." });
    }
}
