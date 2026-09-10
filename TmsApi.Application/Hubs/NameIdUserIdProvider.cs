using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace TmsApi.Api.Hubs;

/// <summary>
/// Custom user ID provider that resolves the user ID from either the long-form
/// ClaimTypes.NameIdentifier or the short-form JWT "nameid" claim.
/// Required because JsonWebTokenHandler (the .NET 10 default) does not map
/// inbound claims, so Context.UserIdentifier would otherwise be null.
/// </summary>
public class NameIdUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("nameid")?.Value;
    }
}
