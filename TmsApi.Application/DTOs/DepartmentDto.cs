namespace TmsApi.Application.DTOs;

public record DepartmentDto(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
