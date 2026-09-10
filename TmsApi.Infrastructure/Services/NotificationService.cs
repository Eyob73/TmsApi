using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Api.Hubs;
using TmsApi.Application.DTOs;
using TmsApi.Application.Hubs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly TmsDbContext _context;
    private readonly IHubContext<TmsHub, ITmsHubClient> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        TmsDbContext context,
        IHubContext<TmsHub, ITmsHubClient> hubContext,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<NotificationDto> CreateAsync(
        string userId, string title, string message, string type,
        string? referenceId = null, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);

        var dto = ToDto(notification);

        // Broadcast to the target user's SignalR group
        try
        {
            _logger.LogInformation("Broadcasting notification {NotificationId} to group {GroupName}", dto.Id, GroupNames.User(userId));
            await _hubContext.Clients.Group(GroupNames.User(userId)).ReceiveNotification(dto);
            _logger.LogInformation("Broadcasted successfully to group {GroupName}", GroupNames.User(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast notification {NotificationId} to user {UserId}", notification.Id, userId);
        }

        return dto;
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(
        string userId, bool unreadOnly = false, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId)
            .AsQueryable();

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => ToDto(n))
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task MarkAsReadAsync(int notificationId, string userId, CancellationToken ct = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification is not null && !notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllAsReadAsync(string userId, CancellationToken ct = default)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.Id, n.UserId, n.Title, n.Message, n.Type, n.ReferenceId, n.IsRead, n.CreatedAt
    );
}
