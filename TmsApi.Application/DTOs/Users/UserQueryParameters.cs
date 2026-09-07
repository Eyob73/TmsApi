namespace TmsApi.Application.DTOs.Users;

public record UserQueryParameters
{
    private const int MaxPageSize = 50;
    private int _pageSize = 20;

    public int Page { get; init; } = 1;

    public int PageSize
    {
        get => _pageSize;
        init =>
            _pageSize =
                value < 1 ? 20
                : value > MaxPageSize ? MaxPageSize
                : value;
    }

    public string? Search { get; init; }
    public string? Role { get; init; }
    public bool? IsActive { get; init; }
    public string OrderBy { get; init; } = "CreatedAt";
    public bool Descending { get; init; } = true;
}
