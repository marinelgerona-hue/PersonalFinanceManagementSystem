using Microsoft.EntityFrameworkCore;
using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

public interface IAdminService
{
    // Dashboard
    AdminStatsViewModel GetStats();

    // User management
    AdminUserListViewModel ListUsers(ListQuery query, AdminUserFilter filter);
    AdminUserDetailsViewModel GetUserDetails(User admin, int userId);
    AdminUserFormViewModel GetUserForm(int userId);
    (bool Success, string Error) CreateUser(AdminUserFormViewModel model, User admin, HttpContext context);
    (bool Success, string Error) UpdateUser(AdminUserFormViewModel model, User admin, HttpContext context);
    (bool Success, string Error) SetActive(int userId, bool active, User admin, HttpContext context);
    (bool Success, string Error) Delete(int userId, User admin, HttpContext context);

    // Category management
    AdminCategoryListViewModel ListCategories(ListQuery query, CategoryType? type);
    AdminCategoryFormViewModel GetCategoryForm(int id);
    (bool Success, string Error) CreateCategory(AdminCategoryFormViewModel model, User admin, HttpContext context);
    (bool Success, string Error) UpdateCategory(AdminCategoryFormViewModel model, User admin, HttpContext context);
    (bool Success, string Error) SetCategoryActive(int id, bool active, User admin, HttpContext context);
    (bool Success, string Error) DeleteCategory(int id, User admin, HttpContext context);
    List<string> GetActiveCategoryNames(CategoryType type);

    // System reports
    AdminReportViewModel GetReport();

    // Activity logs
    AdminActivityListViewModel ListActivity(ListQuery query, int userId, string action);
    (bool Success, string Error) ClearActivity(User admin, HttpContext context);

    // Admin settings
    string UpdateAdminProfile(User admin, AdminSettingsFormViewModel form, HttpContext context);
    bool ChangeAdminPassword(User admin, AdminChangePasswordViewModel form, HttpContext context);
}

/// <summary>
/// Backing services for the Admin Console: account management, the system-wide
/// category catalog, statistics for the dashboard and reports, and the audit
/// trail. Every mutation is validated, logged, and guarded so an admin can
/// never lock themselves — or the last remaining admin — out of the system.
/// </summary>
public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly IActivityLogger _activity;

    public AdminService(ApplicationDbContext db, IActivityLogger activity)
    {
        _db = db;
        _activity = activity;
    }

    // ============================================================ dashboard

    public AdminStatsViewModel GetStats()
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var weekStart = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Sunday);

        var vm = new AdminStatsViewModel
        {
            TotalUsers = _db.Users.Count(),
            TotalAdmins = _db.Users.Count(u => u.Role == UserRoles.Admin),
            ActiveUsers = _db.Users.Count(u => u.IsActive),
            InactiveUsers = _db.Users.Count(u => !u.IsActive),
            NewUsersThisMonth = _db.Users.Count(u => u.CreatedAt >= monthStart),
            TotalCategories = _db.Categories.Count(c => c.IsActive),
            ExpenseCategoryCount = _db.Categories.Count(c => c.Type == CategoryType.Expense && c.IsActive),
            IncomeCategoryCount = _db.Categories.Count(c => c.Type == CategoryType.Income && c.IsActive),
            TotalIncomeRecords = _db.Incomes.Count(),
            TotalExpenseRecords = _db.Expenses.Count(),
            TotalBudgets = _db.Budgets.Count(),
            TotalGoals = _db.SavingsGoals.Count(),
            TotalRecurringDefinitions = _db.RecurringTransactions.Count(),
            RecentSignups = _db.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToList(),
            RecentActivity = _db.ActivityLogs
                .OrderByDescending(l => l.Id)
                .Take(8)
                .ToList()
        };

        // 6-month sign-up trend, including the current month.
        vm.NewUsersThisWeek = _db.Users.Count(u => u.CreatedAt >= weekStart);
        vm.SignupsPerMonth = MonthCounts(
            _db.Users.ToList().GroupBy(u => u.CreatedAt.ToString("yyyy-MM")),
            months: 6);

        return vm;
    }

    /// <summary>Builds an oldest-first list of (label, count) covering the last
    /// <paramref name="months"/> calendar months, zero-filled where nothing happened.</summary>
    private static List<MonthCount> MonthCounts<T>(IEnumerable<IGrouping<string, T>> groups, int months)
    {
        var byMonth = groups.ToDictionary(g => g.Key, g => g.Count());
        var rows = new List<MonthCount>();
        for (var i = months - 1; i >= 0; i--)
        {
            var key = DateTime.Now.AddMonths(-i).ToString("yyyy-MM");
            var label = DateTime.Now.AddMonths(-i).ToString("MMM ''yy");
            rows.Add(new MonthCount(label, byMonth.TryGetValue(key, out var c) ? c : 0));
        }
        return rows;
    }

    // ============================================================ users

    public AdminUserListViewModel ListUsers(ListQuery query, AdminUserFilter filter)
    {
        query.Normalize(new[] { "name", "role", "status", "created", "records" },
            defaultSort: "created", defaultDir: "desc", sizes: new[] { 10, 25, 50, 100 });

        // Preload record counts grouped by user in one pass, then decorate rows.
        // Only counts are used software-side — never the record values themselves.
        var counts = LoadUserCounts();
        var users = _db.Users.ToList();

        var rows = users.Select(u =>
        {
            counts.TryGetValue(u.Id, out var c);
            return new AdminUserRowViewModel
            {
                User = u,
                IncomeRecords = c.IncomeRecords,
                ExpenseRecords = c.ExpenseRecords,
                Budgets = c.Budgets,
                Goals = c.Goals
            };
        });

        var filtered = rows.Where(r => filter.Matches(r.User)).ToList();
        if (query.Search.Length > 0)
        {
            var term = query.Search;
            filtered = filtered
                .Where(r => ListQueryExtensions.AnyContains(term, r.User.FullName, r.User.Email, r.User.Phone))
                .ToList();
        }

        var sorts = new Dictionary<string, Func<AdminUserRowViewModel, object?>>
        {
            ["name"] = r => r.User.FullName,
            ["role"] = r => r.User.Role,
            ["status"] = r => r.User.IsActive,
            ["created"] = r => r.User.CreatedAt,
            ["records"] = r => r.IncomeRecords + r.ExpenseRecords
        };
        var page = filtered.ToPaged(query, sorts);

        return new AdminUserListViewModel
        {
            Query = query,
            Page = page,
            Filter = filter,
            TotalUsers = users.Count,
            TotalAdmins = users.Count(u => u.Role == UserRoles.Admin),
            TotalActive = users.Count(u => u.IsActive),
            TotalInactive = users.Count(u => !u.IsActive)
        };
    }

    /// <summary>Per-user record counts for the list tables — what the admin sees
    /// about accounts. Financial record content stays private to each user.</summary>
    private Dictionary<int, (int IncomeRecords, int ExpenseRecords, int Budgets, int Goals)> LoadUserCounts()
    {
        var incomes = _db.Incomes.GroupBy(i => i.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() }).ToList();
        var expenses = _db.Expenses.GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() }).ToList();
        var budgets = _db.Budgets.GroupBy(b => b.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() }).ToList();
        var goals = _db.SavingsGoals.GroupBy(g => g.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() }).ToList();

        var result = new Dictionary<int, (int, int, int, int)>();
        foreach (var g in incomes) result[g.UserId] = (g.Count, 0, 0, 0);
        foreach (var g in expenses)
        {
            var cur = result.GetValueOrDefault(g.UserId);
            result[g.UserId] = (cur.Item1, g.Count, cur.Item3, cur.Item4);
        }
        foreach (var g in budgets)
        {
            var cur = result.GetValueOrDefault(g.UserId);
            result[g.UserId] = (cur.Item1, cur.Item2, g.Count, cur.Item4);
        }
        foreach (var g in goals)
        {
            var cur = result.GetValueOrDefault(g.UserId);
            result[g.UserId] = (cur.Item1, cur.Item2, cur.Item3, g.Count);
        }
        return result;
    }

    public AdminUserDetailsViewModel GetUserDetails(User admin, int userId)
    {
        var user = _db.Users.FirstOrDefault(u => u.Id == userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var counts = LoadUserCounts().GetValueOrDefault(userId);
        return new AdminUserDetailsViewModel
        {
            User = user,
            IncomeRecords = counts.Item1,
            ExpenseRecords = counts.Item2,
            Budgets = counts.Item3,
            Goals = counts.Item4,
            RecurringDefinitions = _db.RecurringTransactions.Count(r => r.UserId == userId),
            ActivityEntries = _db.ActivityLogs.Count(l => l.UserId == userId),
            RecentActivity = _db.ActivityLogs.Where(l => l.UserId == userId)
                .OrderByDescending(l => l.Id).Take(10).ToList(),
            CurrentAdminId = admin.Id
        };
    }

    public AdminUserFormViewModel GetUserForm(int userId)
    {
        if (userId == 0) return new AdminUserFormViewModel { Currency = "USD" };

        var user = _db.Users.FirstOrDefault(u => u.Id == userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        return new AdminUserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Currency = user.Currency,
            Role = user.Role,
            IsActive = user.IsActive
        };
    }

    public (bool, string) CreateUser(AdminUserFormViewModel model, User admin, HttpContext context)
        => UpsertUser(model, admin, context, create: true);

    public (bool, string) UpdateUser(AdminUserFormViewModel model, User admin, HttpContext context)
        => UpsertUser(model, admin, context, create: false);

    private (bool, string) UpsertUser(AdminUserFormViewModel model, User admin, HttpContext context, bool create)
    {
        var email = model.Email.Trim().ToLowerInvariant();

        if (_db.Users.Any(u => u.Email.ToLower() == email && u.Id != model.Id))
            return (false, "Another account already uses this email.");

        if (model.IsCreate && string.IsNullOrWhiteSpace(model.Password))
            return (false, "Password is required.");

        User user;
        if (create)
        {
            var salt = SecurityHelper.NewSalt();
            user = new User
            {
                PasswordSalt = salt,
                PasswordHash = SecurityHelper.Hash(model.Password!, salt),
                CreatedAt = DateTime.Now
            };
            _db.Users.Add(user);
        }
        else
        {
            user = _db.Users.FirstOrDefault(u => u.Id == model.Id);
            if (user is null)
                return (false, "Account not found.");

            // An admin cannot strip their own admin role or deactivate themselves.
            if (user.Id == admin.Id && !model.IsActive)
                return (false, "You cannot deactivate your own account.");
            if (user.Id == admin.Id && user.Role == UserRoles.Admin && model.Role != UserRoles.Admin)
                return (false, "You cannot remove your own admin role.");

            // At least one admin must remain in the system.
            if (user.Role == UserRoles.Admin && model.Role != UserRoles.Admin &&
                user.Id != admin.Id && _db.Users.Count(u => u.Role == UserRoles.Admin) <= 1)
                return (false, "The last administrator cannot have their role removed.");
        }

        user.FullName = model.FullName.Trim();
        user.Email = email;
        user.Phone = model.Phone?.Trim() ?? string.Empty;
        user.Currency = model.Currency;
        user.Role = model.Role;
        user.IsActive = model.IsActive;

        // Optional password change when editing; required one when creating.
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            user.PasswordSalt = SecurityHelper.NewSalt();
            user.PasswordHash = SecurityHelper.Hash(model.Password!, user.PasswordSalt);
        }

        _db.SaveChanges();

        _activity.Log(context, user.Id, user.FullName,
            create ? "UserCreated" : "UserUpdated",
            create
                ? $"Account created by {admin.FullName} ({user.Email}, role {user.Role})."
                : $"Account details updated by {admin.FullName} ({user.Email}, role {user.Role}).");

        return (true, string.Empty);
    }

    public (bool, string) SetActive(int userId, bool active, User admin, HttpContext context)
    {
        var user = _db.Users.FirstOrDefault(u => u.Id == userId);
        if (user is null)
            return (false, "Account not found.");

        if (user.Id == admin.Id)
            return (false, "You cannot deactivate your own account.");

        // Deactivating the last admin would lock the Admin Console permanently.
        if (user.Role == UserRoles.Admin && _db.Users.Count(u => u.Role == UserRoles.Admin && u.IsActive) <= 1)
            return (false, "The last active administrator cannot be deactivated.");

        user.IsActive = active;
        _db.SaveChanges();

        if (!active)
        {
            // A deactivated account must not keep an active remembered login.
            user.RememberTokenHash = null;
            user.RememberTokenExpires = null;
            _db.SaveChanges();
        }

        _activity.Log(context, user.Id, user.FullName,
            active ? "AccountActivated" : "AccountDeactivated",
            $"Account {(active ? "activated" : "deactivated")} by {admin.FullName} ({user.Email}).");

        return (true, string.Empty);
    }

    public (bool, string) Delete(int userId, User admin, HttpContext context)
    {
        var user = _db.Users.FirstOrDefault(u => u.Id == userId);
        if (user is null)
            return (false, "Account not found.");

        if (user.Id == admin.Id)
            return (false, "You cannot delete your own account.");

        if (user.Role == UserRoles.Admin && _db.Users.Count(u => u.Role == UserRoles.Admin) <= 1)
            return (false, "The last administrator cannot be deleted.");

        // Remove the account's own financial data first (foreign keys are
        // delete-restricted), then the account itself.
        _db.GoalContributions.RemoveRange(_db.GoalContributions.Where(c => c.UserId == userId));
        _db.SavingsGoals.RemoveRange(_db.SavingsGoals.Where(g => g.UserId == userId));
        _db.RecurringTransactions.RemoveRange(_db.RecurringTransactions.Where(r => r.UserId == userId));
        _db.Budgets.RemoveRange(_db.Budgets.Where(b => b.UserId == userId));
        _db.Expenses.RemoveRange(_db.Expenses.Where(e => e.UserId == userId));
        _db.Incomes.RemoveRange(_db.Incomes.Where(i => i.UserId == userId));
        _db.SaveChanges();

        var email = user.Email;
        var name = user.FullName;
        _db.Users.Remove(user);
        _db.SaveChanges();

        _activity.Log(context, null, name, "AccountDeleted",
            $"Account deleted by {admin.FullName} ({email}) — all of the account's financial records were removed with it.");

        return (true, string.Empty);
    }

    // ============================================================ categories

    public AdminCategoryListViewModel ListCategories(ListQuery query, CategoryType? type)
    {
        query.Normalize(new[] { "name", "type", "status", "created" },
            defaultSort: "name", defaultDir: "asc");

        var categories = _db.Categories.AsNoTracking().ToList();

        // How many saved records use each name — delete-protection data.
        var usedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in _db.Incomes.GroupBy(i => i.Source).Select(g => new { g.Key, Count = g.Count() }))
            usedCounts[g.Key] = g.Count;
        foreach (var e in _db.Expenses.GroupBy(e => e.Category).Select(g => new { g.Key, Count = g.Count() }))
            usedCounts[e.Key] = usedCounts.TryGetValue(e.Key, out var c) ? c + e.Count : e.Count;

        var rows = categories.Select(c => new AdminCategoryRowViewModel
        {
            Category = c,
            InUseCount = usedCounts.TryGetValue(c.Name, out var n) ? n : 0
        });

        if (type is not null)
            rows = rows.Where(r => r.Category.Type == type);
        if (query.Search.Length > 0)
        {
            var term = query.Search;
            rows = rows.Where(r => r.Category.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var sorts = new Dictionary<string, Func<AdminCategoryRowViewModel, object?>>
        {
            ["name"] = r => r.Category.Name,
            ["type"] = r => r.Category.Type,
            ["status"] = r => r.Category.IsActive,
            ["created"] = r => r.Category.CreatedAt
        };
        var page = rows.ToList().ToPaged(query, sorts);

        return new AdminCategoryListViewModel
        {
            Query = query,
            Page = page,
            Type = type,
            TotalActive = categories.Count(c => c.IsActive),
            TotalInactive = categories.Count(c => !c.IsActive),
            TotalIncome = categories.Count(c => c.Type == CategoryType.Income),
            TotalExpense = categories.Count(c => c.Type == CategoryType.Expense)
        };
    }

    public AdminCategoryFormViewModel GetCategoryForm(int id)
    {
        if (id == 0) return new AdminCategoryFormViewModel();

        var c = _db.Categories.FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Category {id} not found.");
        return new AdminCategoryFormViewModel { Id = c.Id, Type = c.Type, Name = c.Name, IsActive = c.IsActive };
    }

    public (bool, string) CreateCategory(AdminCategoryFormViewModel model, User admin, HttpContext context)
        => UpsertCategory(model, admin, context, create: true);

    public (bool, string) UpdateCategory(AdminCategoryFormViewModel model, User admin, HttpContext context)
        => UpsertCategory(model, admin, context, create: false);

    private (bool, string) UpsertCategory(AdminCategoryFormViewModel model, User admin, HttpContext context, bool create)
    {
        var name = model.Name.Trim();
        if (name.Length == 0)
            return (false, "Name is required.");

        if (_db.Categories.Any(c => c.Type == model.Type && c.Name == name && c.Id != model.Id))
            return (false, $"A {model.Type.ToString().ToLowerInvariant()} category named “{name}” already exists.");

        Category category;
        if (create)
        {
            category = new Category { Type = model.Type, Name = name, IsActive = model.IsActive };
            _db.Categories.Add(category);
        }
        else
        {
            category = _db.Categories.FirstOrDefault(c => c.Id == model.Id);
            if (category is null)
                return (false, "Category not found.");

            // Renaming a category that existing records use would quietly
            // detach those records from the catalog — block it and explain why.
            if (!category.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && IsCategoryInUse(category.Name))
                return (false,
                    $"This category is used by existing records, so its name cannot change. " +
                    $"Deactivate it and create a new one instead.");

            category.Type = model.Type;
            category.Name = name;
            category.IsActive = model.IsActive;
        }

        _db.SaveChanges();

        _activity.Log(context, admin.Id, admin.FullName,
            create ? "CategoryCreated" : "CategoryUpdated",
            $"{(create ? "Created" : "Updated")} {model.Type.ToString().ToLowerInvariant()} category “{name}”{(model.IsActive ? "" : " (inactive)")}.");

        return (true, string.Empty);
    }

    private bool IsCategoryInUse(string name)
        => _db.Incomes.Any(i => i.Source == name) || _db.Expenses.Any(e => e.Category == name);

    public (bool, string) SetCategoryActive(int id, bool active, User admin, HttpContext context)
    {
        var category = _db.Categories.FirstOrDefault(c => c.Id == id);
        if (category is null)
            return (false, "Category not found.");

        category.IsActive = active;
        _db.SaveChanges();

        _activity.Log(context, admin.Id, admin.FullName,
            active ? "CategoryActivated" : "CategoryDeactivated",
            $"{category.Type} category “{category.Name}” {(active ? "re-activated" : "deactivated")} by {admin.FullName}.");

        return (true, string.Empty);
    }

    public (bool, string) DeleteCategory(int id, User admin, HttpContext context)
    {
        var category = _db.Categories.FirstOrDefault(c => c.Id == id);
        if (category is null)
            return (false, "Category not found.");

        // Records keep the category as plain text; removing the name from the
        // catalog should never orphan or mislabel those records.
        var inUse = IsCategoryInUse(category.Name);
        if (inUse)
            return (false,
                $"“{category.Name}” is still used by existing records — deactivate it instead of deleting it.");

        _db.Categories.Remove(category);
        _db.SaveChanges();

        _activity.Log(context, admin.Id, admin.FullName, "CategoryDeleted",
            $"Deleted {category.Type.ToString().ToLowerInvariant()} category “{category.Name}”.");

        return (true, string.Empty);
    }

    /// <summary>Names users can currently pick from, for the shared user-side
    /// dropdowns. Falls back to the static catalog when nothing is configured.</summary>
    public List<string> GetActiveCategoryNames(CategoryType type)
    {
        var names = _db.Categories
            .Where(c => c.Type == type && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToList();
        return names.Count > 0 ? names : type == CategoryType.Expense
            ? Catalog.ExpenseCategories.ToList()
            : Catalog.IncomeSources.ToList();
    }

    // ============================================================ reports

    public AdminReportViewModel GetReport()
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var users = _db.Users.ToList();

        var vm = new AdminReportViewModel
        {
            TotalUsers = users.Count,
            ActiveUsers = users.Count(u => u.IsActive),
            InactiveUsers = users.Count(u => !u.IsActive),
            TotalAdmins = users.Count(u => u.Role == UserRoles.Admin),
            NewUsersThisMonth = users.Count(u => u.CreatedAt >= monthStart),
            TotalIncomeRecords = _db.Incomes.Count(),
            TotalExpenseRecords = _db.Expenses.Count(),
            TotalBudgets = _db.Budgets.Count(),
            TotalGoals = _db.SavingsGoals.Count(),
            TotalRecurringDefinitions = _db.RecurringTransactions.Count(),
            TotalActivityEntries = _db.ActivityLogs.Count(),
            ActivityEntriesToday = _db.ActivityLogs.Count(l => l.CreatedAt.Date == now.Date)
        };

        vm.SignupsPerMonth = MonthCounts(
            users.GroupBy(u => u.CreatedAt.ToString("yyyy-MM")), months: 12);

        vm.ActivityPerMonth = MonthCounts(
            _db.ActivityLogs.ToList().GroupBy(l => l.CreatedAt.ToString("yyyy-MM")), months: 6);

        // Cumulative user growth per month over the last 12 months.
        var growth = new List<UserGrowthRow>();
        for (var i = 11; i >= 0; i--)
        {
            var monthEnd = new DateTime(now.Year, now.Month, 1).AddMonths(-(i - 1));
            if (i == 0) monthEnd = now;
            var y = monthEnd.AddDays(-1);
            var inMonth = users.Count(u => u.CreatedAt.Year == y.Year && u.CreatedAt.Month == y.Month);
            var totalToDate = users.Count(u => u.CreatedAt < monthEnd);
            growth.Add(new UserGrowthRow(y.Year, y.Month, inMonth, totalToDate));
        }
        vm.UserGrowth = growth;

        return vm;
    }

    // ============================================================ activity logs

    public AdminActivityListViewModel ListActivity(ListQuery query, int userId, string action)
    {
        query.Normalize(new[] { "time", "actor", "action" },
            defaultSort: "time", defaultDir: "desc");

        var entries = _db.ActivityLogs.AsNoTracking();

        if (userId > 0)
            entries = entries.Where(l => l.UserId == userId);
        if (!string.IsNullOrEmpty(action) && action != "all")
            entries = entries.Where(l => l.Action == action);
        if (query.Search.Length > 0)
        {
            var term = query.Search;
            entries = entries.Where(l =>
                l.ActorName.Contains(term) || l.Detail.Contains(term) ||
                (l.IpAddress ?? string.Empty).Contains(term));
        }

        var sorts = new Dictionary<string, Func<ActivityLog, object?>>
        {
            ["time"] = l => l.Id,
            ["actor"] = l => l.ActorName,
            ["action"] = l => l.Action
        };
        var paged = entries.AsEnumerable().ToPaged(query, sorts);

        return new AdminActivityListViewModel
        {
            Query = query,
            Page = paged,
            UserId = userId,
            Action = action,
            Actions = _db.ActivityLogs.Select(l => l.Action).Distinct().OrderBy(a => a).ToList(),
            Users = _db.Users.AsNoTracking()
                .OrderBy(u => u.FullName)
                .Select(u => new ValueTuple<int, string>(u.Id, u.FullName))
                .ToList()
        };
    }

    public (bool, string) ClearActivity(User admin, HttpContext context)
    {
        var removed = _db.ActivityLogs.ExecuteDelete();
        _activity.Log(context, admin.Id, admin.FullName, "ActivityCleared",
            $"Cleared {removed} activity log entries.");

        return (true, string.Empty);
    }

    // ============================================================ admin settings

    public string UpdateAdminProfile(User admin, AdminSettingsFormViewModel form, HttpContext context)
    {
        var email = form.Email.Trim().ToLowerInvariant();

        if (_db.Users.Any(u => u.Email.ToLower() == email && u.Id != admin.Id))
            return "Another account already uses this email.";

        admin.FullName = form.FullName.Trim();
        admin.Email = email;
        admin.Phone = form.Phone?.Trim() ?? string.Empty;
        _db.SaveChanges();

        _activity.Log(context, admin.Id, admin.FullName, "AdminProfileUpdated",
            "Administrator account details updated.");

        return string.Empty;
    }

    public bool ChangeAdminPassword(User admin, AdminChangePasswordViewModel form, HttpContext context)
    {
        if (!SecurityHelper.Verify(form.CurrentPassword, admin.PasswordSalt, admin.PasswordHash))
            return false;

        admin.PasswordSalt = SecurityHelper.NewSalt();
        admin.PasswordHash = SecurityHelper.Hash(form.NewPassword, admin.PasswordSalt);
        _db.SaveChanges();

        _activity.Log(context, admin.Id, admin.FullName, "AdminPasswordChanged",
            "Administrator password changed.");

        return true;
    }
}
