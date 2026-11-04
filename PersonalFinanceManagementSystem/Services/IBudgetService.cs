using PersonalFinanceManagementSystem.Models;
using PersonalFinanceManagementSystem.Models.ViewModels;

namespace PersonalFinanceManagementSystem.Services;

public interface IBudgetService
{
    List<Budget> GetAll(int userId);
    List<Budget> GetForMonth(int userId, int year, int month);
    Budget? Get(int userId, int id);
    /// <summary>Adds a budget. Returns an error message when an identical budget already exists.</summary>
    string Add(Budget budget);
    /// <summary>Updates a budget. Returns an error message when it would duplicate another budget.</summary>
    string Update(Budget budget);
    /// <summary>Deletes a budget and clears the deducted-from link on any expense pointed at it.</summary>
    bool Delete(int userId, int id);
    /// <summary>
    /// Budgets whose period window overlaps the given calendar month, each
    /// enriched with spent/remaining/percent. Spent is recalculated live from
    /// expenses: an expense is counted for at most one budget — the one it is
    /// assigned to, or (when unassigned) any same-category budget whose
    /// period contains the expense date.
    /// </summary>
    List<BudgetStatus> GetStatusForMonth(int userId, int year, int month);
    /// <summary>Expenses counted by one budget, using the same category,
    /// date-range and explicit-assignment rules as its live spent total.</summary>
    List<Expense> GetExpensesForBudget(int userId, Budget budget);
    /// <summary>
    /// The automatic deduction target for an expense: the most specific
    /// (daily > weekly > monthly > yearly) budget of the same category whose
    /// period window contains the expense date.
    /// </summary>
    Budget? MatchForExpense(int userId, string category, DateTime date);
    /// <summary>
    /// All of the user's budgets as dropdown options for the expense form,
    /// flagged as IsMatch when they would be picked automatically for the
    /// given category and expense date.
    /// </summary>
    List<BudgetOption> GetOptionsForExpense(int userId, string? category, DateTime date);
}
