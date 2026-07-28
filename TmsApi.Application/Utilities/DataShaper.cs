using System.Reflection;
using TmsApi.Application.Common;

namespace TmsApi.Application.Utilities;

public static class DataShaper
{
    public static IEnumerable<Dictionary<string, object?>> ShapeData<T>(
        this IEnumerable<T> source,
        string? fields,
        ISet<string> allowedFields
    )
    {
        var properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => allowedFields.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (string.IsNullOrWhiteSpace(fields))
            return source.Select(e => properties.ToDictionary(p => p.Name, p => p.GetValue(e)));

        var requested = fields
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var unknown = requested
            .Where(f => !allowedFields.Contains(f, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (unknown.Count > 0)
            throw new BadRequestException(
                $"Unknown field(s): {string.Join(", ", unknown)}. "
                    + $"Allowed fields: {string.Join(", ", allowedFields)}."
            );

        var picked = properties
            .Where(p => requested.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return source.Select(e => picked.ToDictionary(p => p.Name, p => p.GetValue(e)));
    }
}
