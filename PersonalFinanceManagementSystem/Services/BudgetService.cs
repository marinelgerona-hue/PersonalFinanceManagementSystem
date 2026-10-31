using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// CRUD for budgets with Start Date / End Date schedules.
/// Spent is calculated live from the database on every read, so expenses
/// added, edited or deleted are always reflected without any stored counter
/// to get out of sync — which also prevents duplicate deductions: each
/// expense counts for at most one budget (its assigned one, or a
/// same-category budget whose date range contains the expense date).
/// </summary>
public class BudgetService : IBudgetService
{
    private readonly ApplicationDbContext _db;

    public BudgetService(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Budget> GetAll(int userId)
    {
        return _db.Budgets
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.StartDate) // StartDate/EndDate are real columns now — sortable in SQL
            .ThenBy(b => b.Category)
            .ToList();
    }

    public List<Budget> GetForMonth(int userId, int year, int month)
    {
        // Budgets whose Start Date / End Date window touches this calendar
        // month. StartDate and EndDate are plain columns, so the overlap
        // check translates to SQL directly.
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        return _db.Budgets
            .Where(b =>
                b.UserId == userId &&
                b.StartDate <= monthEnd &&
                b.EndDate >= monthStart)
            .OrderBy(b => b.StartDate)
            .ThenBy(b => b.Category)
            .ToList();
    }

    public Budget? Get(int userId, int id)
    {
        return _db.Budgets
            .FirstOrDefault(b =>
                b.UserId == userId &&
                b.Id == id);
    }

    public string Add(Budget budget)
    {
        // Same category whose date window overlaps the new budget's window.
        // Both bounds are plain columns, so this stays translatable to SQL.
        var duplicate = _db.Budgets.Any(b =>
            b.UserId == budget.UserId &&
            b.Category.ToLower() == budget.Category.ToLower() &&
            b.StartDate <= budget.EndDate &&
            b.EndDate >= budget.StartDate);

        if (duplicate)
        {
            return $"A budget for {budget.Category} already exists for this period.";
        }

        _db.Budgets.Add(budget);
        _db.SaveChanges();

        return string.Empty;
    }

    public string Update(Budget budget)
    {
        var existing = _db.Budgets
            .FirstOrDefault(b =>
                b.UserId == budget.UserId &&
                b.Id == budget.Id);

        if (existing is null)
        {
            return "Budget not found.";
        }

        // Same duplicate rule as Add — see there for the shape.
        var duplicate = _db.Budgets.Any(b =>
            b.UserId == budget.UserId &&
            b.Id != budget.Id &&
            b.Category.ToLower() == budget.Category.ToLower() &&
            b.StartDate <= budget.EndDate &&
            b.EndDate >= budget.StartDate);

        if (duplicate)
        {
            return $"A budget for {budget.Category} already exists for this period.";
        }

        existing.Category = budget.Category;
        existing.Limit = budget.Limit;
        existing.StartDate = budget.StartDate;
        existing.EndDate = budget.EndDate;

        _db.SaveChanges();

        return string.Empty;
    }

    public bool Delete(int userId, int id)
    {
        var existing = _db.Budgets
            .FirstOrDefault(b =>
                b.UserId == userId &&
                b.Id == id);

        if (existing is null)
        {
            return false;
        }

        // Expenses pointed at this budget fall back to category + date-range
        // matching again, so their amounts stay deducted from whichever
        // budget still covers them instead of dangling off a deleted row.
        var linked = _db.Expenses.Where(e => e.BudgetId == id);
        foreach (var expense in linked)
        {
            expense.BudgetId = null;
        }

        _db.Budgets.Remove(existing);
        _db.SaveChanges();

        return true;
    }

    public List<BudgetStatus> GetStatusForMonth(
        int userId,
        int year,
        int month)
    {
        var budgets = GetForMonth(userId, year, month);

        if (budgets.Count == 0)
        {
            return new List<BudgetStatus>();
        }

        // One expense query over the widest window span; each budget's
        // spent amount is then matched in memory against its own range.
        var from = budgets.Min(b => b.StartDate);
        var to = budgets.Max(b => b.EndDate);
        var toExclusive = to.Date.AddDays(1);

        var expenses = _db.Expenses
            .Where(e =>
                e.UserId == userId &&
                e.Date >= from &&
                e.Date < toExclusive)
            .ToList();

        return budgets.Select(b =>
        {
            var spent = expenses
                .Where(e => Counts(b, e))
                .Sum(e => e.Amount);

            return new BudgetStatus
            {
                Budget = b,
                Spent = spent,
                Remaining = b.Limit - spent,
                PercentUsed = b.Limit > 0
                    ? Math.Round(spent / b.Limit * 100, 1)
                    : 0
            };
        }).ToList();
    }

    public Budget? MatchForExpense(int userId, string category, DateTime date)
    {
        var dateValue = date.Date;

        // All budgets of the same category whose window contains the expense
        // date (straight SQL — StartDate/EndDate are columns). The most
        // specific window wins (smallest span), ties go to the latest start.
        return _db.Budgets
            .Where(b =>
                b.UserId == userId &&
                b.Category.ToLower() == category.ToLower() &&
                b.StartDate <= dateValue &&
                b.EndDate >= dateValue)
            .ToList()
            .OrderBy(b => b.EndDate.Subtract(b.StartDate).Days)
            .ThenByDescending(b => b.StartDate)
            .FirstOrDefault();
    }

    public List<BudgetOption> GetOptionsForExpense(
        int userId,
        string? category,
        DateTime date)
    {
        var candidates = _db.Budgets
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.StartDate)
            .ThenBy(b => b.Category)
            .ToList();

        return candidates
            .Select(b => new BudgetOption
            {
                Id = b.Id,
                Category = b.Category,
                Start = b.StartDate,
                End = b.EndDate,
                IsMatch = !string.IsNullOrEmpty(category)
                    && b.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                    && b.Covers(date)
            })
            .ToList();
    }

    // ------------------------------------------------------------ internals

    /// <summary>
    /// Whether an expense is deducted from a budget: same category, inside
    /// the budget's Start Date / End Date window, and — the duplicate-
    /// deduction guard — either unassigned (falls in by category and date)
    /// or assigned to
    /// this very budget. An expense assigned to a different budget of the
    /// same category is never counted twice.
    /// </summary>
    private static bool Counts(Budget budget, Expense expense)
    {
        return expense.Category.Equals(budget.Category, StringComparison.OrdinalIgnoreCase)
            && expense.Date.Date >= budget.StartDate.Date
            && expense.Date.Date <= budget.EndDate.Date
            && (expense.BudgetId is null || expense.BudgetId == budget.Id);
    }
}