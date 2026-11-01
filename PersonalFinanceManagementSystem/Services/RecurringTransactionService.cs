using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

public class RecurringTransactionService : IRecurringTransactionService
{
    /// <summary>Safety cap on how many occurrences one item generates per call,
    /// so a pathological backlog (e.g. a Daily item started years ago) can't hang
    /// a page load. Any remainder self-heals over the next few requests.</summary>
    private const int MaxIterationsPerItem = 500;

    private readonly ApplicationDbContext _db;

    public RecurringTransactionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<RecurringTransaction> GetAll(int userId)
    {
        return _db.RecurringTransactions
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.NextDueDate)
            .ToList();
    }

    public RecurringTransaction? Get(int userId, int id)
    {
        return _db.RecurringTransactions
            .FirstOrDefault(r => r.UserId == userId && r.Id == id);
    }

    public RecurringTransaction Add(RecurringTransaction item)
    {
        _db.RecurringTransactions.Add(item);
        _db.SaveChanges();
        return item;
    }

    public bool Update(RecurringTransaction item)
    {
        var existing = _db.RecurringTransactions
            .FirstOrDefault(r => r.UserId == item.UserId && r.Id == item.Id);
        if (existing is null) return false;

        existing.Type = item.Type;
        existing.Label = item.Label;
        existing.Amount = item.Amount;
        existing.Frequency = item.Frequency;
        existing.StartDate = item.StartDate;
        existing.NextDueDate = item.NextDueDate;
        existing.EndDate = item.EndDate;
        existing.IsActive = item.IsActive;
        existing.Note = item.Note ?? string.Empty;
        existing.IncomePool = item.IncomePool;
        existing.BudgetId = item.BudgetId;

        _db.SaveChanges();
        return true;
    }

    public bool Delete(int userId, int id)
    {
        var existing = _db.RecurringTransactions
            .FirstOrDefault(r => r.UserId == userId && r.Id == id);
        if (existing is null) return false;

        _db.RecurringTransactions.Remove(existing);
        _db.SaveChanges();
        return true;
    }

    public int GenerateDueOccurrences(int userId)
    {
        var today = DateTime.Today;
        var due = _db.RecurringTransactions
            .Where(r => r.UserId == userId && r.IsActive && r.NextDueDate.Date <= today)
            .ToList();
        if (due.Count == 0) return 0;

        var generated = 0;
        foreach (var r in due)
        {
            var iterations = 0;
            while (r.NextDueDate.Date <= today
                   && (r.EndDate is null || r.NextDueDate.Date <= r.EndDate.Value.Date)
                   && iterations++ < MaxIterationsPerItem)
            {
                if (r.Type == RecurringTransactionType.Income)
                {
                    _db.Incomes.Add(new Income
                    {
                        UserId = r.UserId,
                        Source = r.Label,
                        Amount = r.Amount,
                        Period = MapToIncomePeriod(r.Frequency),
                        Date = r.NextDueDate.Date,
                        Note = r.Note,
                        RecurringTransactionId = r.Id
                    });
                }
                else
                {
                    _db.Expenses.Add(new Expense
                    {
                        UserId = r.UserId,
                        Category = r.Label,
                        Amount = r.Amount,
                        Date = r.NextDueDate.Date,
                        Note = r.Note,
                        IncomePool = r.IncomePool,
                        BudgetId = r.BudgetId,
                        RecurringTransactionId = r.Id
                    });
                }

                generated++;
                r.NextDueDate = Advance(r.NextDueDate, r.Frequency, r.StartDate.Day);
            }

            if (r.EndDate is not null && r.NextDueDate.Date > r.EndDate.Value.Date)
            {
                r.IsActive = false;
            }
        }

        _db.SaveChanges();
        return generated;
    }

    public bool SkipNext(int userId, int id)
    {
        var r = Get(userId, id);
        if (r is null) return false;

        r.NextDueDate = Advance(r.NextDueDate, r.Frequency, r.StartDate.Day);
        _db.SaveChanges();
        return true;
    }

    public List<RecurringTransaction> GetUpcoming(int userId, int withinDays)
    {
        var cutoff = DateTime.Today.AddDays(withinDays);
        return _db.RecurringTransactions
            .Where(r => r.UserId == userId && r.IsActive && r.NextDueDate.Date <= cutoff)
            .OrderBy(r => r.NextDueDate)
            .ToList();
    }

    public List<RecentTransaction> GetHistory(int userId, int recurringTransactionId)
    {
        var incomeRows = _db.Incomes
            .Where(i => i.UserId == userId && i.RecurringTransactionId == recurringTransactionId)
            .Select(i => new RecentTransaction
            {
                Date = i.Date,
                Title = i.Source,
                Category = i.Source,
                Amount = i.Amount,
                IsIncome = true
            });

        var expenseRows = _db.Expenses
            .Where(e => e.UserId == userId && e.RecurringTransactionId == recurringTransactionId)
            .Select(e => new RecentTransaction
            {
                Date = e.Date,
                Title = string.IsNullOrWhiteSpace(e.Note) ? e.Category : e.Note,
                Category = e.Category,
                Amount = e.Amount,
                IsIncome = false
            });

        return incomeRows.Concat(expenseRows)
            .OrderByDescending(t => t.Date)
            .ToList();
    }

    // ------------------------------------------------------------ date math

    /// <summary>Daily/Weekly/Biweekly: plain day arithmetic. Monthly/Yearly: re-derives
    /// the target day from <paramref name="anchorDay"/> every step (never from the
    /// previously-clamped date), so a Jan 31 bill produces Feb 28 → Mar 31 → Apr 30 → May 31,
    /// not a degraded 28/28/28 sequence.</summary>
    private static DateTime Advance(DateTime date, RecurrenceFrequency freq, int anchorDay) => freq switch
    {
        RecurrenceFrequency.Daily => date.AddDays(1),
        RecurrenceFrequency.Weekly => date.AddDays(7),
        RecurrenceFrequency.Biweekly => date.AddDays(14),
        RecurrenceFrequency.Yearly => AddYearClamped(date, anchorDay),
        _ => AddMonthClamped(date, anchorDay)
    };

    private static DateTime AddMonthClamped(DateTime date, int anchorDay)
    {
        var totalMonths = date.Year * 12 + (date.Month - 1) + 1;
        var year = totalMonths / 12;
        var month = totalMonths % 12 + 1;
        return new DateTime(year, month, Math.Min(anchorDay, DateTime.DaysInMonth(year, month)));
    }

    private static DateTime AddYearClamped(DateTime date, int anchorDay)
    {
        var year = date.Year + 1;
        var month = date.Month;
        return new DateTime(year, month, Math.Min(anchorDay, DateTime.DaysInMonth(year, month)));
    }

    private static IncomePeriod MapToIncomePeriod(RecurrenceFrequency freq) => freq switch
    {
        RecurrenceFrequency.Daily => IncomePeriod.Daily,
        RecurrenceFrequency.Weekly => IncomePeriod.Weekly,
        RecurrenceFrequency.Biweekly => IncomePeriod.Weekly,
        RecurrenceFrequency.Yearly => IncomePeriod.Yearly,
        _ => IncomePeriod.Monthly
    };
}
