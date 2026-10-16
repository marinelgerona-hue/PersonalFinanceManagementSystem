using System.Globalization;

namespace PersonalFinanceManagementSystem.Helpers;

/// <summary>Formatting helpers for money, percentages, dates and colors (used by views).</summary>
public static class Format
{
    private static readonly string[] Palette =
    {
        "#10b981", "#6366f1", "#f59e0b", "#0ea5e9", "#ec4899",
        "#8b5cf6", "#ef4444", "#14b8a6", "#f97316", "#3b82f6", "#64748b"
    };

    public static string Symbol(string? currency) => currency switch
    {
        "PHP" => "₱",
        "EUR" => "€",
        "GBP" => "£",
        "INR" => "₹",
        "PKR" => "₨",
        "LKR" => "Rs ",
        "AED" => "AED ",
        "CAD" => "C$",
        "AUD" => "A$",
        _ => "$"
    };

    public static string Money(decimal amount, string? currency = "USD")
        => Symbol(currency) + amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string Money0(decimal amount, string? currency = "USD")
        => Symbol(currency) + amount.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Number formatted with invariant culture — safe to embed in CSS/SVG attributes.</summary>
    public static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Number formatted with invariant culture — safe to embed in CSS/SVG attributes.</summary>
    public static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Percent formatted with invariant culture — safe to embed in CSS/SVG attributes.</summary>
    public static string Pct(decimal value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>Percent formatted with invariant culture — safe to embed in CSS/SVG attributes.</summary>
    public static string Pct(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    public static string Date(DateTime date) => date.ToString("MMM d, yyyy");

    public static string MonthName(int month) => new DateTime(2000, month, 1).ToString("MMMM", CultureInfo.InvariantCulture);

    public static string MonthShort(int month) => new DateTime(2000, month, 1).ToString("MMM", CultureInfo.InvariantCulture);

    /// <summary>Stable color for a category/source name (same color every run, every process).</summary>
    public static string CategoryColor(string name)
    {
        unchecked
        {
            var h = 0;
            foreach (var ch in name) h = (h * 31 + ch) & 0xffffff;
            return Palette[h % Palette.Length];
        }
    }
}
