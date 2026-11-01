using System.Text.Json;

namespace PersonalFinanceManagementSystem.Helpers;

/// <summary>
/// Builds the JSON payload for a chart tooltip, rendered by site.js from a <c>data-tooltip</c> attribute:
/// <code>&lt;div data-tooltip="@ChartTip.Build("Oct 2026", ("Income", "$6,440.00", "#10b981"))"&gt;</code>
/// Razor HTML-encodes the attribute, and the browser decodes it back before site.js parses it.
/// </summary>
public static class ChartTip
{
    public static string Build(string title, params (string Label, string Value, string? Color)[] rows)
        => JsonSerializer.Serialize(new
        {
            title,
            rows = rows.Select(r => new { label = r.Label, value = r.Value, color = r.Color })
        });
}
