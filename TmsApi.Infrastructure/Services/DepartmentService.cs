using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class DepartmentService(TmsDbContext context) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await context
            .Departments.AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(
                d.Id,
                d.Name,
                d.Code,
                d.Description,
                d.IsActive,
                d.CreatedAt,
                d.UpdatedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<DepartmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context
            .Departments.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DepartmentDto(
                d.Id,
                d.Name,
                d.Code,
                d.Description,
                d.IsActive,
                d.CreatedAt,
                d.UpdatedAt
            ))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<DepartmentDto> CreateAsync(
        CreateDepartmentRequest request,
        CancellationToken ct = default
    )
    {
        var normalizedName = request.Name.Trim();

        if (await NameExistsAsync(normalizedName, ct))
        {
            throw new InvalidOperationException(
                $"A department named '{normalizedName}' already exists."
            );
        }

        var entity = new Department
        {
            Name = normalizedName,
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        await context.Departments.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);

        return MapToDto(entity);
    }

    public async Task<DepartmentDto> UpdateAsync(
        Guid id,
        UpdateDepartmentRequest request,
        CancellationToken ct = default
    )
    {
        var entity =
            await context.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException($"Department '{id}' was not found.");

        var normalizedName = request.Name.Trim();

        if (await context.Departments.AnyAsync(d => d.Id != id && d.Name == normalizedName, ct))
        {
            throw new InvalidOperationException(
                $"A department named '{normalizedName}' already exists."
            );
        }

        entity.Name = normalizedName;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<DepartmentDto> PatchAsync(
        Guid id,
        PatchDepartmentRequest request,
        CancellationToken ct = default
    )
    {
        var entity =
            await context.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException($"Department '{id}' was not found.");

        if (request.Name is not null)
        {
            var normalizedName = request.Name.Trim();
            if (await context.Departments.AnyAsync(d => d.Id != id && d.Name == normalizedName, ct))
            {
                throw new InvalidOperationException(
                    $"A department named '{normalizedName}' already exists."
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
        return await context.Departments.AnyAsync(d => d.Name == normalizedName, ct);
    }

    private static DepartmentDto MapToDto(Department entity)
    {
        return new DepartmentDto(
            entity.Id,
            entity.Name,
            entity.Code,
            entity.Description,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}
