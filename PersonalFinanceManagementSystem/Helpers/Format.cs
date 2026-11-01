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

    /// <summary>Currency amount; negatives put the sign before the symbol ("-$38.00", not "$-38.00").</summary>
    public static string Money(decimal amount, string? currency = "USD")
        => (amount < 0 ? "-" : "") + Symbol(currency) + Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);

    public static string Money0(decimal amount, string? currency = "USD")
        => (amount < 0 ? "-" : "") + Symbol(currency) + Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A count (records, users, …) with thousands separators, invariant culture.</summary>
    public static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

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

    /// <summary>Fixed, visually distinct colors for the built-in expense categories and income sources.</summary>
    private static readonly Dictionary<string, string> KnownColors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Expense categories
        ["Housing"] = "#6366f1", ["Groceries"] = "#10b981", ["Transportation"] = "#f59e0b",
        ["Utilities"] = "#0ea5e9", ["Dining"] = "#f97316", ["Entertainment"] = "#ec4899",
        ["Shopping"] = "#8b5cf6", ["Health"] = "#ef4444", ["Education"] = "#14b8a6",
        ["Travel"] = "#3b82f6", ["Other"] = "#94a3b8",
        // Income sources
        ["Salary"] = "#059669", ["Freelance"] = "#0284c7", ["Business"] = "#4f46e5",
        ["Investments"] = "#7c3aed", ["Rental"] = "#d97706", ["Bonus"] = "#db2777", ["Gift"] = "#ea580c"
    };

    /// <summary>Stable color for a category/source name (same color every run, every process).</summary>
    public static string CategoryColor(string name)
    {
        if (KnownColors.TryGetValue(name, out var known)) return known;
        unchecked
        {
            var h = 0;
            foreach (var ch in name) h = (h * 31 + ch) & 0xffffff;
            return Palette[h % Palette.Length];
        }
    }
}
