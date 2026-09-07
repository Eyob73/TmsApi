namespace TmsApi.Application.DTOs;

public record ProgramDto(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    Guid? DepartmentId,
    string? DepartmentName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
