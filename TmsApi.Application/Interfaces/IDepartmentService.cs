using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct = default);
    Task<DepartmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DepartmentDto> CreateAsync(
        CreateDepartmentRequest request,
        CancellationToken ct = default
    );
    Task<DepartmentDto> UpdateAsync(
        Guid id,
        UpdateDepartmentRequest request,
        CancellationToken ct = default
    );
    Task<DepartmentDto> PatchAsync(
        Guid id,
        PatchDepartmentRequest request,
        CancellationToken ct = default
    );
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
}
