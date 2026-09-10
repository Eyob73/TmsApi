using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(string userId, string title, string message, string type, string? referenceId = null, CancellationToken ct = default);
    Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, bool unreadOnly = false, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);
    Task MarkAsReadAsync(int notificationId, string userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(string userId, CancellationToken ct = default);
}
