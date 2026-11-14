using Microsoft.EntityFrameworkCore;
using MarksApi.Dtos;

namespace MarksApi.Infrastructure;

public static class Paging
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;
    private const int MaxPage = 1_000_000;

    /// <summary>Corrects out-of-range values. The server never trusts the client's paging numbers.</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var p = page is null or < 1 ? 1 : Math.Min(page.Value, MaxPage);
        var s = pageSize is null or < 1 ? DefaultPageSize : Math.Min(pageSize.Value, MaxPageSize);
        return (p, s);
    }

    /// <summary>Parses "field" or "-field" (descending). Unknown fields are rejected with a 400.</summary>
    public static (string Field, bool Descending) ParseSort(string? sort, string defaultField, IReadOnlyCollection<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(sort)) return (defaultField, false);

        var trimmed = sort.Trim();
        var desc = trimmed.StartsWith('-');
        var field = (desc ? trimmed[1..] : trimmed).ToLowerInvariant();

        if (!allowed.Contains(field))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["sort"] = [$"Sort must be one of: {string.Join(", ", allowed)} (prefix with - for descending)."]
            });
        }
        return (field, desc);
    }

    /// <summary>Escapes LIKE wildcards so a search for "50%" is literal.</summary>
    public static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page, pageSize, total);
    }
}
