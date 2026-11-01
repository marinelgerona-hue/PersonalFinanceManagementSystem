using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

public interface IExpenseService
{
    List<Expense> GetAll(int userId);
    /// <summary>Total expenses deducted from each income period
    /// (pool), computed live so add/edit/delete of expense records is
    /// always reflected. One slice per Daily/Weekly/Monthly/Yearly.
    /// Expenses with no pool (records saved before pools existed)
    /// belong to no slice.</summary>
    List<PoolSpentSlice> TotalsByPool(int userId);
    Expense? Get(int userId, int id);
    Expense Add(Expense expense);
    /// <summary>Inserts several expenses in a single round trip (e.g. CSV import).</summary>
    void AddRange(IEnumerable<Expense> expenses);
    bool Update(Expense expense);
    bool Delete(int userId, int id);
    decimal Total(int userId);
    decimal TotalForMonth(int userId, int year, int month);
    List<Expense> ForMonth(int userId, int year, int month);
    /// <summary>All expenses whose Date falls between from and to (inclusive).</summary>
    List<Expense> ForRange(int userId, DateTime from, DateTime to);
    /// <summary>Spending grouped by category for one month, largest first.</summary>
    List<CategorySlice> SpendingByCategory(int userId, int year, int month);
    /// <summary>Top categories across an entire year (for the annual report).</summary>
    List<CategorySlice> SpendingByCategoryForYear(int userId, int year, int top);
    Expense? LargestInMonth(int userId, int year, int month);

    /// <summary>Available income-pool balance: all income of the chosen
    /// period recorded on or before asOf, minus expenses already deducted
    /// from that pool on or before asOf. excludingExpenseId lets an edit
    /// pretend its own old deduction was not yet applied.</summary>
    decimal IncomePoolAvailable(int userId, IncomePeriod pool, DateTime asOf, int? excludingExpenseId);
}
