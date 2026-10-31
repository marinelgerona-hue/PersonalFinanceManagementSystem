namespace PersonalFinanceManagementSystem.Models;

/// <summary>Whether a catalog category applies to income sources or expense categories.</summary>
public enum CategoryType
{
    Income = 0,
    Expense = 1
}

/// <summary>
/// An admin-managed catalog entry backing the income source and expense category
/// choices users pick from when adding records. Existing records keep whatever
/// category name they were saved with; the catalog only governs new choices.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public CategoryType Type { get; set; }

    /// <summary>Name shown in dropdowns; shared across the whole system.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Inactive entries stop appearing in user-side dropdowns but stay in the catalog.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// One recorded system activity (user sign-in, admin change, …) for the
/// Admin Console's Activity Logs page. Only actions worth auditing are written.
/// </summary>
public class ActivityLog
{
    public int Id { get; set; }

    /// <summary>The user whose action this was, when the action can be attributed
    /// to a specific account (null for events like a rejected sign-in attempt).</summary>
    public int? UserId { get; set; }

    /// <summary>Who performed it, denormalized from the user row so the log stays
    /// readable even if the account is later deleted.</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Short machine-friendly event name, e.g. "Login", "UserDeactivated".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Human-readable description of what happened.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Client IP the request came from, when available.</summary>
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
