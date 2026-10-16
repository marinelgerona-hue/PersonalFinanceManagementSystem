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
}
