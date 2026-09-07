using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class ProgramService(TmsDbContext context) : IProgramService
{
    public async Task<IReadOnlyList<ProgramDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await context
            .Programs.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProgramDto(
                p.Id,
                p.Name,
                p.Code,
                p.Description,
                p.DepartmentId,
                p.Department != null ? p.Department.Name : null,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProgramDto>> GetByDepartmentAsync(
        Guid departmentId,
        CancellationToken ct = default
    )
    {
        return await context
            .Programs.AsNoTracking()
            .Where(p => p.DepartmentId == departmentId)
            .OrderBy(p => p.Name)
            .Select(p => new ProgramDto(
                p.Id,
                p.Name,
                p.Code,
                p.Description,
                p.DepartmentId,
                p.Department != null ? p.Department.Name : null,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<ProgramDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context
            .Programs.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProgramDto(
                p.Id,
                p.Name,
                p.Code,
                p.Description,
                p.DepartmentId,
                p.Department != null ? p.Department.Name : null,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt
            ))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<ProgramDto> CreateAsync(
        CreateProgramRequest request,
        CancellationToken ct = default
    )
    {
        var normalizedName = request.Name.Trim();

        if (await NameExistsAsync(normalizedName, ct))
        {
            throw new InvalidOperationException(
                $"A program named '{normalizedName}' already exists."
            );
        }

        if (request.DepartmentId.HasValue)
        {
            var departmentExists = await context.Departments.AnyAsync(
                d => d.Id == request.DepartmentId.Value,
                ct
            );

            if (!departmentExists)
            {
                throw new InvalidOperationException(
                    $"Department '{request.DepartmentId.Value}' does not exist."
                );
            }
        }

        var entity = new Program
        {
            Name = normalizedName,
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            DepartmentId = request.DepartmentId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        await context.Programs.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<ProgramDto> UpdateAsync(
        Guid id,
        UpdateProgramRequest request,
        CancellationToken ct = default
    )
    {
        var entity =
            await context.Programs.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Program '{id}' was not found.");

        var normalizedName = request.Name.Trim();

        if (await context.Programs.AnyAsync(p => p.Id != id && p.Name == normalizedName, ct))
        {
            throw new InvalidOperationException(
                $"A program named '{normalizedName}' already exists."
            );
        }

        if (request.DepartmentId.HasValue)
        {
            var departmentExists = await context.Departments.AnyAsync(
                d => d.Id == request.DepartmentId.Value,
                ct
            );

            if (!departmentExists)
            {
                throw new InvalidOperationException(
                    $"Department '{request.DepartmentId.Value}' does not exist."
                );
            }
        }

        entity.Name = normalizedName;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        entity.DepartmentId = request.DepartmentId;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<ProgramDto> PatchAsync(
        Guid id,
        PatchProgramRequest request,
        CancellationToken ct = default
    )
    {
        var entity =
            await context.Programs.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Program '{id}' was not found.");

        if (request.Name is not null)
        {
            var normalizedName = request.Name.Trim();
            if (await context.Programs.AnyAsync(p => p.Id != id && p.Name == normalizedName, ct))
            {
                throw new InvalidOperationException(
                    $"A program named '{normalizedName}' already exists."
                );
            }

            entity.Name = normalizedName;
        }

        if (request.Code is not null)
        {
            entity.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        }

        if (request.Description is not null)
        {
            entity.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
        }

        if (request.DepartmentId is not null)
        {
            var departmentExists = await context.Departments.AnyAsync(
                d => d.Id == request.DepartmentId.Value,
                ct
            );

            if (!departmentExists)
            {
                throw new InvalidOperationException(
                    $"Department '{request.DepartmentId.Value}' does not exist."
                );
            }

            entity.DepartmentId = request.DepartmentId;
        }

        if (request.IsActive is not null)
        {
            entity.IsActive = request.IsActive.Value;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken ct = default)
    {
        var normalizedName = name.Trim();
        return await context.Programs.AnyAsync(p => p.Name == normalizedName, ct);
    }

    private ProgramDto MapToDto(Program entity)
    {
        return new ProgramDto(
            entity.Id,
            entity.Name,
            entity.Code,
            entity.Description,
            entity.DepartmentId,
            entity.Department != null ? entity.Department.Name : null,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}
