using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs.Users;

public record UpdateUserRolesRequest
{
    [Required(ErrorMessage = "Roles list is required.")]
    public IReadOnlyList<string> Roles { get; init; } = [];

    public UpdateUserRolesRequest() { }

    public UpdateUserRolesRequest(IReadOnlyList<string> roles)
    {
        Roles = roles;
    }
}
