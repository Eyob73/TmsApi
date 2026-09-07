namespace TmsApi.Application.DTOs.Users;

public record UpdateUserStatusRequest
{
    public bool IsActive { get; init; }

    public UpdateUserStatusRequest() { }

    public UpdateUserStatusRequest(bool isActive)
    {
        IsActive = isActive;
    }
}
