using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

public class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _db;

    public ExpenseService(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Expense> GetAll(int userId)
    {
        return _db.Expenses
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .ToList();
    }

    public Expense? Get(int userId, int id)
    {
        return _db.Expenses
            .FirstOrDefault(e => e.UserId == userId && e.Id == id);
    }

    public Expense Add(Expense expense)
    {
        _db.Expenses.Add(expense);
        _db.SaveChanges();

        return expense;
    }

    public void AddRange(IEnumerable<Expense> expenses)
    {
        _db.Expenses.AddRange(expenses);
        _db.SaveChanges();
    }

    public bool Update(Expense expense)
    {
        var existing = _db.Expenses
            .FirstOrDefault(e =>
                e.UserId == expense.UserId &&
                e.Id == expense.Id);

        if (existing is null)
            return false;

        existing.Category = expense.Category;
        existing.Amount = expense.Amount;
        existing.Date = expense.Date;
        existing.Note = expense.Note ?? string.Empty;

        // Re-point the expense at the budget chosen in the form (it may have
        // moved to a different period). Spent/remaining amounts of both the
        // old and the new budget recalculate live on the next read.
        existing.BudgetId = expense.BudgetId;

        // Re-point the expense at the income pool chosen in the form.
        existing.IncomePool = expense.IncomePool;

        _db.SaveChanges();

        return true;
    }

    public bool Delete(int userId, int id)
    {
        var existing = _db.Expenses
            .FirstOrDefault(e =>
                e.UserId == userId &&
                e.Id == id);

        if (existing is null)
            return false;

        _db.Expenses.Remove(existing);
        _db.SaveChanges();

        return true;
    }

    public decimal Total(int userId)
    {
        return _db.Expenses
            .Where(e => e.UserId == userId)
            .Sum(e => e.Amount);
    }

    public decimal TotalForMonth(int userId, int year, int month)
    {
        return _db.Expenses
            .Where(e =>
                e.UserId == userId &&
                e.Date.Year == year &&
                e.Date.Month == month)
            .Sum(e => e.Amount);
    }

    public List<Expense> ForMonth(int userId, int year, int month)
    {
        return _db.Expenses
            .Where(e =>
                e.UserId == userId &&
                e.Date.Year == year &&
                e.Date.Month == month)
            .OrderByDescending(e => e.Date)
            .ToList();
    }

    public List<Expense> ForRange(int userId, DateTime from, DateTime to)
    {
        var toExclusive = to.Date.AddDays(1);

        return _db.Expenses
            .Where(e =>
                e.UserId == userId &&
                e.Date >= from.Date &&
                e.Date < toExclusive)
            .OrderByDescending(e => e.Date)
            .ToList();
    }

    // Spending grouped by category for one month
    public List<CategorySlice> SpendingByCategory(
        int userId,
        int year,
        int month)
    {
        return _db.Expenses
            .Where(e =>
                e.UserId == userId &&
                e.Date.Year == year &&
                e.Date.Month == month)
            .GroupBy(e => e.Category)
            .Select(g => new CategorySlice
            {
                Category = g.Key,
                Amount = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ToList()
            .WithPercentOf(null);
    }

    // Top spending categories for an entire year
    public List<CategorySlice> SpendingByCategoryForYear(
        int userId,
        int year,
        int top)
    {
        var slices = _db.Expenses
            .Where(e =>
                e.UserId == userId &&
                e.Date.Year == year)
            .GroupBy(e => e.Category)
            .Select(g => new CategorySlice
            {
                Category = g.Key,
                Amount = g.Sum(e => e.Amount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        // Percent is a share of the whole year, not just of the top N shown.
        var yearTotal = slices.Sum(x => x.Amount);
        return slices.Take(top).ToList().WithPercentOf(yearTotal);
    }

    // Largest expense for one month
    public Expense? LargestInMonth(
        int userId,
        int year,
        int month)
    {
        return _db.Expenses
            .Where(e =>
                e.UserId == userId &&
                e.Date.Year == year &&
                e.Date.Month == month)
            .OrderByDescending(e => e.Amount)
            .FirstOrDefault();
    }

    // ------------------------------------------------------------
    // Available balance of one income pool as of a date
    // ------------------------------------------------------------

    // Income rows of the chosen period received on or before asOf credit the
    // pool; expenses drawn from the pool on or before asOf debit it. The
    // period/pool filtering happens in memory so the enum-to-int conversion
    // never has to translate into MySQL; the date filter stays in SQL.
    public decimal IncomePoolAvailable(
        int userId,
        IncomePeriod pool,
        DateTime asOf,
        int? excludingExpenseId)
    {
        var endExclusive = asOf.Date.AddDays(1);

        var income = _db.Incomes
            .Where(i => i.UserId == userId && i.Date < endExclusive)
            .Select(i => new { i.Period, i.Amount })
            .ToList()
            .Where(i => i.Period == pool)
            .Sum(i => i.Amount);

        var spent = _db.Expenses
            .Where(e => e.UserId == userId && e.Date < endExclusive)
            .Select(e => new { e.IncomePool, e.Amount })
            .ToList()
            .Where(e => e.IncomePool == pool)
            .Sum(e => e.Amount);

        if (excludingExpenseId is int excludeId)
        {
            var own = _db.Expenses
                .Where(e => e.UserId == userId && e.Id == excludeId)
                .Select(e => new { e.IncomePool, e.Amount })
                .ToList()
                .Where(e => e.IncomePool == pool)
                .Sum(e => e.Amount);

            spent -= own;
        }

        return income - spent;
    }
}

internal static class CategorySliceExtensions
{
    /// <summary>Fills each slice's Percent as its share of <paramref name="total"/>
    /// (or of the slices' own sum when total is null).</summary>
    public static List<CategorySlice> WithPercentOf(this List<CategorySlice> slices, decimal? total)
    {
        var sum = total ?? slices.Sum(x => x.Amount);
        foreach (var s in slices)
        {
            s.Percent = sum > 0 ? Math.Round(s.Amount / sum * 100, 1) : 0;
        }
        return slices;
    }
}
