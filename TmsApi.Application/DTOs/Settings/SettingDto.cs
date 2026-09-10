using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs.Settings;

public class SettingDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateSettingDto
{
    [Required]
    public string Value { get; set; } = string.Empty;
}
