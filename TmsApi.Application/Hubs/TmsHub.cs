using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Hubs;

namespace TmsApi.Api.Hubs;

[Authorize]
public class TmsHub : Hub<ITmsHubClient>
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var logger = httpContext?.RequestServices.GetService<ILogger<TmsHub>>();

        // Join user-specific group for targeted notifications using authenticated user
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            logger?.LogInformation("SignalR connected: mapping Context.ConnectionId {ConnectionId} to User {UserId}", Context.ConnectionId, userId);
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.User(userId));
        }
        else
        {
            logger?.LogWarning("SignalR connected: Context.UserIdentifier is NULL for connection {ConnectionId}", Context.ConnectionId);
        }

        // Legacy: join student group
        var studentId = httpContext?.Request.Query["studentId"].ToString();
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Student(studentId));
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinCourseGroup(string courseCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Course(courseCode));
    }

    public async Task LeaveCourseGroup(string courseCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNames.Course(courseCode));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // SignalR removes the connection from all groups automatically.
        await base.OnDisconnectedAsync(exception);
    }
}

public static class GroupNames
{
    public static string User(string userId) => $"user-{userId}";
    public static string Student(string studentId) => $"student-{studentId}";
    public static string Course(string courseCode) => $"course-{courseCode}";
}
