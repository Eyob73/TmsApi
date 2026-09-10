using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface IProgramService
{
    Task<IReadOnlyList<ProgramDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProgramDto>> GetByDepartmentAsync(
        Guid departmentId,
        CancellationToken ct = default
    );
    Task<ProgramDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProgramDto> CreateAsync(CreateProgramRequest request, CancellationToken ct = default);
    Task<ProgramDto> UpdateAsync(
        Guid id,
        UpdateProgramRequest request,
        CancellationToken ct = default
    );
    Task<ProgramDto> PatchAsync(
        Guid id,
        PatchProgramRequest request,
        CancellationToken ct = default
    );
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
}
