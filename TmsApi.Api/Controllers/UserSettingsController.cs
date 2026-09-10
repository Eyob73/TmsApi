using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs.Settings;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/user-settings")]
[Authorize]
public class UserSettingsController : ControllerBase
{
    private readonly TmsDbContext _context;

    public UserSettingsController(TmsDbContext context)
    {
        _context = context;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? string.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SettingDto>>> GetSettings()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var settings = await _context.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => new SettingDto
            {
                Key = s.Key,
                Value = s.Value
            })
            .ToListAsync();

        return Ok(settings);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingDto dto)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var setting = await _context.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key);

        if (setting == null)
        {
            setting = new UserSetting
            {
                UserId = userId,
                Key = key,
                Value = dto.Value,
                CreatedAt = DateTime.UtcNow
            };
            _context.UserSettings.Add(setting);
        }
        else
        {
            setting.Value = dto.Value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
