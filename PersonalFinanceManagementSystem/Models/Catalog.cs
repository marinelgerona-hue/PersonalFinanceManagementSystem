namespace PersonalFinanceManagementSystem.Models;

/// <summary>Fixed catalogs of choices used across the app (categories, sources, currencies).</summary>
public static class Catalog
{
    public static readonly string[] ExpenseCategories =
    {
        "Housing", "Groceries", "Transportation", "Utilities", "Dining", "Entertainment",
        "Shopping", "Health", "Education", "Travel", "Other"
    };

    public static readonly string[] IncomeSources =
    {
        "Salary", "Freelance", "Business", "Investments", "Rental", "Bonus", "Gift", "Other"
    };

    public static readonly (string Code, string Name)[] Currencies =
    {
        ("PHP", "Philippine Peso (₱)"),
        ("USD", "US Dollar ($)"),
        ("EUR", "Euro (€)"),
        ("GBP", "British Pound (£)"),
        ("PKR", "Pakistani Rupee (₨)"),
        ("INR", "Indian Rupee (₹)"),
        ("AED", "UAE Dirham"),
        ("CAD", "Canadian Dollar (C$)"),
        ("AUD", "Australian Dollar (A$)")
    };

    /// <summary>Countries offered in the phone number's country-code picker.</summary>
    public static readonly (string Iso, string Name, string DialCode, string Flag)[] PhoneCountries =
    {
        ("US", "United States", "+1", "🇺🇸"),
        ("CA", "Canada", "+1", "🇨🇦"),
        ("PH", "Philippines", "+63", "🇵🇭"),
        ("IN", "India", "+91", "🇮🇳"),
        ("PK", "Pakistan", "+92", "🇵🇰"),
        ("GB", "United Kingdom", "+44", "🇬🇧"),
        ("AE", "United Arab Emirates", "+971", "🇦🇪"),
        ("AU", "Australia", "+61", "🇦🇺"),
        ("DE", "Germany", "+49", "🇩🇪"),
        ("FR", "France", "+33", "🇫🇷"),
        ("IT", "Italy", "+39", "🇮🇹"),
        ("ES", "Spain", "+34", "🇪🇸"),
        ("NL", "Netherlands", "+31", "🇳🇱"),
        ("IE", "Ireland", "+353", "🇮🇪"),
        ("SA", "Saudi Arabia", "+966", "🇸🇦"),
        ("QA", "Qatar", "+974", "🇶🇦"),
        ("KW", "Kuwait", "+965", "🇰🇼"),
        ("SG", "Singapore", "+65", "🇸🇬"),
        ("MY", "Malaysia", "+60", "🇲🇾"),
        ("ID", "Indonesia", "+62", "🇮🇩"),
        ("HK", "Hong Kong", "+852", "🇭🇰"),
        ("CN", "China", "+86", "🇨🇳"),
        ("JP", "Japan", "+81", "🇯🇵"),
        ("KR", "South Korea", "+82", "🇰🇷"),
        ("BD", "Bangladesh", "+880", "🇧🇩"),
        ("NP", "Nepal", "+977", "🇳🇵"),
        ("LK", "Sri Lanka", "+94", "🇱🇰"),
        ("NZ", "New Zealand", "+64", "🇳🇿"),
        ("ZA", "South Africa", "+27", "🇿🇦"),
        ("NG", "Nigeria", "+234", "🇳🇬"),
        ("KE", "Kenya", "+254", "🇰🇪"),
        ("BR", "Brazil", "+55", "🇧🇷"),
        ("MX", "Mexico", "+52", "🇲🇽")
    };

    /// <summary>Joins a country (ISO code) and a national number into the stored form, e.g. "+91 9876543210".
    /// Returns an empty string when no digits were entered.</summary>
    public static string ComposePhone(string? iso, string? national)
    {
        var digits = new string((national ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return string.Empty;

        var country = PhoneCountries.FirstOrDefault(c => c.Iso == iso);
        var dial = country.DialCode ?? PhoneCountries[0].DialCode;
        return $"{dial} {digits}";
    }

    /// <summary>Splits a stored phone number back into (ISO code, national digits) for editing.
    /// Matches the longest dial code; "+1" resolves to the first listed country (United States).</summary>
    public static (string Iso, string National) SplitPhone(string? phone)
    {
        var value = (phone ?? string.Empty).Trim();
        if (value.StartsWith('+'))
        {
            var compact = "+" + new string(value.Where(char.IsDigit).ToArray());
            var match = PhoneCountries
                .Where(c => compact.StartsWith(c.DialCode))
                .OrderByDescending(c => c.DialCode.Length)
                .FirstOrDefault();
            if (match.Iso is not null)
                return (match.Iso, compact[match.DialCode.Length..]);
        }
        return (PhoneCountries[0].Iso, new string(value.Where(char.IsDigit).ToArray()));
    }
}
