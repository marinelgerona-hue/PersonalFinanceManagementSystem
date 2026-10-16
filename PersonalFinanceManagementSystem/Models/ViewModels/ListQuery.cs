namespace PersonalFinanceManagementSystem.Models.ViewModels;

/// <summary>
/// Paging, sorting and search options for a listing, bound from the query string:
/// <c>?page=2&amp;size=25&amp;sort=amount&amp;dir=asc&amp;q=rent</c>.
/// Call <see cref="Normalize"/> before use so bad or tampered values fall back to safe defaults.
/// </summary>
public class ListQuery
{
    public static readonly int[] DefaultSizes = { 10, 25, 50, 100 };

    public int Page { get; set; } = 1;
    public int Size { get; set; }
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public string? Q { get; set; }

    /// <summary>Allowed page sizes for this listing (first one is the default).</summary>
    public int[] SizeOptions { get; private set; } = DefaultSizes;

    public bool Desc => Dir == "desc";
    public string Search => (Q ?? string.Empty).Trim();

    public ListQuery Normalize(IEnumerable<string> sortKeys, string defaultSort, string defaultDir = "desc", int[]? sizes = null)
    {
        SizeOptions = sizes ?? DefaultSizes;
        if (!SizeOptions.Contains(Size)) Size = SizeOptions[0];
        if (Page < 1) Page = 1;

        var keys = sortKeys.ToList();
        if (Sort is null || !keys.Contains(Sort))
        {
            Sort = defaultSort;
            Dir = defaultDir;
        }
        if (Dir != "asc" && Dir != "desc") Dir = defaultDir;
        Q = Search.Length > 100 ? Search[..100] : Search;
        return this;
    }
}

/// <summary>One page of results plus the numbers the pager needs.</summary>
public class PagedList<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int Size { get; init; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Size));
    public int From => TotalCount == 0 ? 0 : (Page - 1) * Size + 1;
    public int To => Math.Min(Page * Size, TotalCount);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    /// <summary>What the shared _Pagination partial needs, independent of the item type.</summary>
    public PagerModel ToPager(ListQuery query, string noun = "records")
        => new(Page, TotalPages, From, To, TotalCount, Size, query.SizeOptions, noun);
}

/// <summary>Model for Views/Shared/_Pagination.cshtml.</summary>
public record PagerModel(int Page, int TotalPages, int From, int To, int TotalCount, int Size, int[] SizeOptions, string Noun)
{
    /// <summary>Page numbers to show, with 0 meaning "…" (e.g. 1 … 4 5 6 … 12).</summary>
    public IEnumerable<int> Window()
    {
        if (TotalPages <= 7) { for (var i = 1; i <= TotalPages; i++) yield return i; yield break; }
        var start = Math.Max(2, Page - 1);
        var end = Math.Min(TotalPages - 1, Page + 1);
        if (Page <= 3) { start = 2; end = 4; }
        if (Page >= TotalPages - 2) { start = TotalPages - 3; end = TotalPages - 1; }
        yield return 1;
        if (start > 2) yield return 0;
        for (var i = start; i <= end; i++) yield return i;
        if (end < TotalPages - 1) yield return 0;
        yield return TotalPages;
    }
}

public static class ListQueryExtensions
{
    /// <summary>
    /// Filters (when a search term is given), sorts by the query's sort key and returns the requested page.
    /// <paramref name="sorts"/> maps each allowed sort key to the value it orders by.
    /// </summary>
    public static PagedList<T> ToPaged<T>(
        this IEnumerable<T> source,
        ListQuery query,
        IReadOnlyDictionary<string, Func<T, object?>> sorts,
        Func<T, string, bool>? matches = null)
    {
        var rows = source;
        if (matches is not null && query.Search.Length > 0)
            rows = rows.Where(r => matches(r, query.Search));

        var key = sorts[query.Sort!];
        rows = query.Desc ? rows.OrderByDescending(key) : rows.OrderBy(key);

        var list = rows.ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)query.Size));
        var page = Math.Min(query.Page, totalPages); // e.g. after deleting the last row of the last page
        query.Page = page;

        return new PagedList<T>
        {
            Items = list.Skip((page - 1) * query.Size).Take(query.Size).ToList(),
            TotalCount = list.Count,
            Page = page,
            Size = query.Size
        };
    }

    /// <summary>Case-insensitive "contains" across several text fields.</summary>
    public static bool AnyContains(string term, params string?[] fields)
        => fields.Any(f => f is not null && f.Contains(term, StringComparison.OrdinalIgnoreCase));
}
