namespace TmsApi.Application.DTOs;

public record NotificationDto(
    int Id,
    string UserId,
    string Title,
    string Message,
    string Type,
    string? ReferenceId,
    bool IsRead,
    DateTime CreatedAt
);
