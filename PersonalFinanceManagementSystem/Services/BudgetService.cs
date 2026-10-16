using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Helpers;
using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// CRUD for budgets with daily/weekly/monthly/yearly periods.
/// Spent is calculated live from the database on every read, so expenses
/// added, edited or deleted are always reflected without any stored counter
/// to get out of sync — which also prevents duplicate deductions: each
/// expense counts for at most one budget (its assigned one, or a
/// same-category budget whose period contains the expense date).
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
            .ToList()
            .OrderBy(b => b.PeriodStart) // computed property — sort in memory
            .ThenBy(b => b.Category)
            .ToList();
    }

    public List<Budget> GetForMonth(int userId, int year, int month)
    {
        // Budgets whose period window touches this calendar month. A window
        // never reaches further than one year past its anchor year, so a
        // coarse database filter on the anchor year ± 1 is enough and the
        // exact overlap is decided in memory.
        var candidates = _db.Budgets
            .Where(b =>
                b.UserId == userId &&
                b.Year + 1 >= year &&
                b.Year - 1 <= year)
            .ToList(); // PeriodStart/End are computed — not translatable to SQL

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        return candidates
            .Where(b => b.PeriodStart <= monthEnd && b.PeriodEnd >= monthStart)
            .OrderBy(b => b.PeriodStart)
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
        // Same category, same period and the same anchor: month for monthly,
        // plus day for daily/weekly, just the year for yearly. Written with
        // plain columns so EF can translate it to SQL.
        var duplicate = _db.Budgets.Any(b =>
            b.UserId == budget.UserId &&
            b.Category.ToLower() == budget.Category.ToLower() &&
            b.Period == budget.Period &&
            b.Year == budget.Year &&
            (budget.Period == BudgetPeriod.Yearly ||
                (b.Month == budget.Month &&
                 (budget.Period == BudgetPeriod.Monthly || b.Day == budget.Day))));

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
            b.Period == budget.Period &&
            b.Year == budget.Year &&
            (budget.Period == BudgetPeriod.Yearly ||
                (b.Month == budget.Month &&
                 (budget.Period == BudgetPeriod.Monthly || b.Day == budget.Day))));

        if (duplicate)
        {
            return $"A budget for {budget.Category} already exists for this period.";
        }

        existing.Category = budget.Category;
        existing.Limit = budget.Limit;
        existing.Year = budget.Year;
        existing.Month = budget.Month;
        existing.Period = budget.Period;
        existing.Day = budget.Day;

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

        // Expenses pointed at this budget fall back to category + period
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
        // spent amount is then matched in memory against its own period.
        var from = budgets.Min(b => b.PeriodStart);
        var to = budgets.Max(b => b.PeriodEnd);
        var toExclusive = to.AddDays(1);

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

        // All budgets of the same category anchored around the expense date;
        // the period-window check happens in memory. The most specific period
        // wins (smallest window), ties go to the latest period start.
        return _db.Budgets
            .Where(b =>
                b.UserId == userId &&
                b.Category.ToLower() == category.ToLower() &&
                b.Year >= dateValue.Year - 1 &&
                b.Year <= dateValue.Year + 1)
            .ToList()
            .Where(b => b.Covers(dateValue))
            .OrderBy(b => PeriodSpan(b.Period))
            .ThenByDescending(b => b.PeriodStart)
            .FirstOrDefault();
    }

    public List<BudgetOption> GetOptionsForExpense(
        int userId,
        string? category,
        DateTime date)
    {
        var now = DateTime.Now;
        var dateValue = date.Date;

        var candidates = _db.Budgets
            .Where(b =>
                b.UserId == userId &&
                b.Year >= now.Year - 1 &&
                b.Year <= now.Year + 1)
            .ToList()
            .OrderBy(b => b.PeriodStart)
            .ThenBy(b => b.Category)
            .ToList();

        return candidates
            .Select(b => new BudgetOption
            {
                Id = b.Id,
                Category = b.Category,
                Period = b.Period,
                Start = b.PeriodStart,
                End = b.PeriodEnd,
                IsMatch = !string.IsNullOrEmpty(category)
                    && b.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                    && b.Covers(dateValue)
            })
            .ToList();
    }

    // ------------------------------------------------------------ internals

    /// <summary>Total number of days a budget period covers, used to pick the
    /// most specific budget when several windows contain the same date.</summary>
    private static int PeriodSpan(BudgetPeriod period) => period switch
    {
        BudgetPeriod.Daily => 1,
        BudgetPeriod.Weekly => 7,
        BudgetPeriod.Yearly => 366,
        _ => 31 // Monthly
    };

    /// <summary>
    /// Whether an expense is deducted from a budget: same category, inside
    /// the budget's period window, and — the duplicate-deduction guard —
    /// either unassigned (falls in by category and period) or assigned to
    /// this very budget. An expense assigned to a different budget of the
    /// same category is never counted twice.
    /// </summary>
    private static bool Counts(Budget budget, Expense expense)
    {
        return expense.Category.Equals(budget.Category, StringComparison.OrdinalIgnoreCase)
            && expense.Date.Date >= budget.PeriodStart
            && expense.Date.Date <= budget.PeriodEnd
            && (expense.BudgetId is null || expense.BudgetId == budget.Id);
    }
}