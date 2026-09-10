using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs.Settings;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/system-settings")]
[Authorize(Roles = "Admin")]
public class SystemSettingsController : ControllerBase
{
    private readonly TmsDbContext _context;

    public SystemSettingsController(TmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SettingDto>>> GetSettings()
    {
        var settings = await _context.SystemSettings
            .Select(s => new SettingDto
            {
                Key = s.Key,
                Value = s.Value,
                Description = s.Description
            })
            .ToListAsync();

        return Ok(settings);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingDto dto)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);

        if (setting == null)
        {
            // Create if doesn't exist
            setting = new SystemSetting
            {
                Key = key,
                Value = dto.Value,
                CreatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(setting);
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
