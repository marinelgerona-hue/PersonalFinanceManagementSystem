using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

public interface IRecurringTransactionService
{
    List<RecurringTransaction> GetAll(int userId);
    RecurringTransaction? Get(int userId, int id);
    RecurringTransaction Add(RecurringTransaction item);
    bool Update(RecurringTransaction item);
    bool ToggleActive(int userId, int id);
    bool Delete(int userId, int id);

    /// <summary>Generates every Income/Expense row for occurrences whose NextDueDate
    /// is on or before today, backfilling all missed periods, and advances
    /// NextDueDate past today. Cheap no-op when nothing is due. Never throws.</summary>
    int GenerateDueOccurrences(int userId);
    int GenerateDueOccurrencesForAllUsers();

    /// <summary>Advances NextDueDate one period forward without generating a row.</summary>
    bool SkipNext(int userId, int id);

    /// <summary>Active items with NextDueDate within the window (overdue items included),
    /// ordered soonest first. Backs both the notification bell and the dashboard widget.</summary>
    List<RecurringTransaction> GetUpcoming(int userId, int withinDays);

    /// <summary>Generated Income/Expense history for one recurring item, newest first.</summary>
    List<RecentTransaction> GetHistory(int userId, int recurringTransactionId);
}
