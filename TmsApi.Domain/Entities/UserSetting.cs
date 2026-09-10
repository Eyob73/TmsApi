namespace TmsApi.Domain.Entities;

public class UserSetting
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual TmsUser? User { get; set; }
}
