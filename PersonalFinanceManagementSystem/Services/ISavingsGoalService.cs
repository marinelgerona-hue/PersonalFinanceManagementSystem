using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

public interface ISavingsGoalService
{
    List<SavingsGoal> GetAll(int userId);
    SavingsGoal? Get(int userId, int id);
    SavingsGoal Add(SavingsGoal goal);
    /// <summary>Updates name, target amount, deadline and note (saved amount
    /// and its contributions are preserved). Returns false if not found.</summary>
    bool Update(int userId, SavingsGoal goal);
    bool Delete(int userId, int id);
    /// <summary>Adds money to a goal and records the contribution. Returns false when the goal was not found.</summary>
    bool AddFunds(int userId, int goalId, decimal amount, DateTime date, string note);
    /// <summary>Goal rows enriched with progress percent / remaining.</summary>
    List<GoalProgress> GetProgress(int userId);
    decimal SavedInMonth(int userId, int year, int month);
    /// <summary>Total contributions whose Date falls between from and to (inclusive).</summary>
    decimal SavedInRange(int userId, DateTime from, DateTime to);
    /// <summary>Contribution totals for the last N months, oldest first.</summary>
    List<MonthAmount> MonthlyContributions(int userId, int months);
    List<GoalContribution> ContributionsFor(int userId, int goalId);
}
