using System.ComponentModel.DataAnnotations;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

/// <summary>One month in the sign-up trend chart.</summary>
public record MonthCount(string Label, int Count);

/// <summary>Top row of the Admin Dashboard: headline system statistics.</summary>
public class AdminStatsViewModel
{
    public int TotalUsers { get; set; }
    public int TotalAdmins { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int NewUsersThisWeek { get; set; }

    public int TotalCategories { get; set; }
    public int ExpenseCategoryCount { get; set; }
    public int IncomeCategoryCount { get; set; }

    public int TotalIncomeRecords { get; set; }
    public int TotalExpenseRecords { get; set; }
    public int TotalBudgets { get; set; }
    public int TotalGoals { get; set; }
    public int TotalRecurringDefinitions { get; set; }

    /// <summary>Sign-ups per month, oldest first (last 6 months incl. current).</summary>
    public List<MonthCount> SignupsPerMonth { get; set; } = new();

    /// <summary>The first few latest-registered accounts.</summary>
    public List<User> RecentSignups { get; set; } = new();

    /// <summary>The newest audit-trail entries.</summary>
    public List<ActivityLog> RecentActivity { get; set; } = new();
}

/// <summary>Filter used on the Users list page: preserved in the query string.</summary>
public record AdminUserFilter(string? Role, string? Status)
{
    /// <summary>Whether this row matches the "Role" and "Status" query filters.</summary>
    public bool Matches(User user)
        => (string.IsNullOrEmpty(Role) || Role == "all" || user.Role == Role)
        && (string.IsNullOrEmpty(Status) || Status == "all" || (Status == "active" ? user.IsActive : !user.IsActive));

    public string ToQueryString() =>
        string.Join("&", new[]
        {
            string.IsNullOrEmpty(Role) ? null : $"role={Role}",
            string.IsNullOrEmpty(Status) ? null : $"status={Status}"
        }.Where(s => s is not null));
}

/// <summary>One row of the Admin Users table.</summary>
public class AdminUserRowViewModel
{
    public required User User { get; init; }
    public int IncomeRecords { get; init; }
    public int ExpenseRecords { get; init; }
    public int Budgets { get; init; }
    public int Goals { get; init; }
}

/// <summary>List page model for Admin → Users.</summary>
public class AdminUserListViewModel
{
    public required ListQuery Query { get; init; }
    public required PagedList<AdminUserRowViewModel> Page { get; init; }
    public required AdminUserFilter Filter { get; init; }

    public int TotalUsers { get; init; }
    public int TotalAdmins { get; init; }
    public int TotalActive { get; init; }
    public int TotalInactive { get; init; }
}

/// <summary>Details page model for a single user account (aggregates only — the
/// admin never sees the user's individual financial records).</summary>
public class AdminUserDetailsViewModel
{
    public required User User { get; init; }
    public int IncomeRecords { get; init; }
    public int ExpenseRecords { get; init; }
    public int Budgets { get; init; }
    public int Goals { get; init; }
    public int RecurringDefinitions { get; init; }
    public int ActivityEntries { get; init; }
    public List<ActivityLog> RecentActivity { get; init; } = new();

    /// <summary>The signed-in admin operating on this account (guards shown in the UI).</summary>
    public int CurrentAdminId { get; init; }
}

/// <summary>Create / Edit form for an account, on the Admin side.</summary>
public class AdminUserFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(320)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Phone (optional)")]
    [StringLength(40)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Currency is required.")]
    public string Currency { get; set; } = "USD";

    [Required]
    public string Role { get; set; } = UserRoles.User;

    public bool IsActive { get; set; } = true;

    /// <summary>Required only when creating; optional (blank = unchanged) when editing.
    /// When editing, administrators set a brand-new password from here.</summary>
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    /// <summary>Second password field so a typo can't silently mis-set an account password.</summary>
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }

    public bool IsCreate => Id == 0;
}

/// <summary>Create / Edit form for a catalog category.</summary>
public class AdminCategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Type is required.")]
    public CategoryType Type { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Keep the name at most 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool IsCreate => Id == 0;
}

/// <summary>List page model for Admin → Categories.</summary>
public class AdminCategoryListViewModel
{
    public required ListQuery Query { get; init; }
    public required PagedList<AdminCategoryRowViewModel> Page { get; init; }

    /// <summary>"Income", "Expense" or null for all.</summary>
    public CategoryType? Type { get; init; }

    public int TotalActive { get; init; }
    public int TotalInactive { get; init; }
    public int TotalIncome { get; init; }
    public int TotalExpense { get; init; }
}

/// <summary>One row of the Admin Categories table.</summary>
public class AdminCategoryRowViewModel
{
    public required Category Category { get; init; }
    public int InUseCount { get; init; }
}

/// <summary>
/// List page model for Admin → Activity Logs, with the audit actions offered
/// as filter options and (optionally) a target user.
/// </summary>
public class AdminActivityListViewModel
{
    public required ListQuery Query { get; init; }
    public required PagedList<ActivityLog> Page { get; init; }

    /// <summary>When set, only entries for this user are shown (0 = everyone).</summary>
    public int UserId { get; set; }

    /// <summary>When set, only entries with this action name are shown ("all" = everything).</summary>
    public string Action { get; set; } = "all";

    /// <summary>Distinct action names present in the log, for the filter dropdown.</summary>
    public List<string> Actions { get; set; } = new();

    /// <summary>Account picker for the user filter: (Id, FullName) — plus an "(unknown)" entry.</summary>
    public List<(int Id, string Name)> Users { get; set; } = new();
}

/// <summary>System Reports page: overall statistics across every account.</summary>
public class AdminReportViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int TotalAdmins { get; set; }

    public int TotalIncomeRecords { get; set; }
    public int TotalExpenseRecords { get; set; }
    public int TotalBudgets { get; set; }
    public int TotalGoals { get; set; }
    public int TotalRecurringDefinitions { get; set; }

    public int TotalActivityEntries { get; set; }
    public int ActivityEntriesToday { get; set; }
    public int NewUsersThisMonth { get; set; }

    /// <summary>Sign-ups per month, oldest first (last 12 months incl. current).</summary>
    public List<MonthCount> SignupsPerMonth { get; set; } = new();

    /// <summary>Activity entries per month, oldest first (last 6 months incl. current).</summary>
    public List<MonthCount> ActivityPerMonth { get; set; } = new();

    /// <summary>Users joined per month, with their records — the "user growth" table.</summary>
    public List<UserGrowthRow> UserGrowth { get; set; } = new();
}

/// <summary>One row of the system report's sign-up trend.</summary>
public record UserGrowthRow(int Year, int Month, int NewUsers, int TotalUsersToDate);

/// <summary>Forms for Admin Settings (admin's own account details).</summary>
public class AdminSettingsViewModel
{
    public AdminSettingsFormViewModel Profile { get; set; } = new();
    public AdminChangePasswordViewModel ChangePassword { get; set; } = new();
    public User Admin { get; set; } = new();
    public string? ProfileError { get; set; }
    public string? PasswordError { get; set; }
}

public class AdminSettingsFormViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(320)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Phone (optional)")]
    [StringLength(40)]
    public string? Phone { get; set; }
}

public class AdminChangePasswordViewModel
{
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your new password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
