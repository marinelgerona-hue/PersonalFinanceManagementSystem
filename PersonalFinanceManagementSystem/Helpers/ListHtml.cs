using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using PersonalFinanceManagementSystem.Models.ViewModels;
using System.Net;

namespace PersonalFinanceManagementSystem.Helpers;

/// <summary>Links for listings that keep every other query-string value (filters, month, period…).</summary>
public static class ListHtml
{
    /// <summary>The current page's URL with some query values replaced (null removes a value).</summary>
    public static string WithQuery(this HttpRequest request, params (string Key, string? Value)[] changes)
    {
        var values = request.Query.ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes)
        {
            if (string.IsNullOrEmpty(value)) values.Remove(key);
            else values[key] = value;
        }
        var path = request.PathBase + request.Path;
        return values.Count == 0 ? path : QueryHelpers.AddQueryString(path, values);
    }

    /// <summary>
    /// A sortable column header. Clicking toggles the direction; a new column starts with
    /// <paramref name="firstDir"/> (e.g. "desc" for amounts and dates, "asc" for names).
    /// </summary>
    public static IHtmlContent SortHeader(this IHtmlHelper html, ListQuery query, string key, string label,
        string firstDir = "asc", string? cssClass = null)
    {
        var request = html.ViewContext.HttpContext.Request;
        var active = query.Sort == key;
        var nextDir = active ? (query.Desc ? "asc" : "desc") : firstDir;
        var url = request.WithQuery(("sort", key), ("dir", nextDir), ("page", null));
        var ariaSort = active ? (query.Desc ? "descending" : "ascending") : "none";
        var icon = active ? (query.Desc ? "i-sort-down" : "i-sort-up") : "i-sort";

        return new HtmlString(
            $"<th class=\"sortable {(active ? "is-sorted " : "")}{cssClass}\" aria-sort=\"{ariaSort}\">" +
            $"<a href=\"{WebUtility.HtmlEncode(url)}\" title=\"Sort by {WebUtility.HtmlEncode(label.ToLowerInvariant())}\">" +
            $"<span>{WebUtility.HtmlEncode(label)}</span><svg class=\"ico-sm sort-ico\"><use href=\"#{icon}\"/></svg></a></th>");
    }
}
