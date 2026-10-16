using PersonalFinanceManagementSystem.Data;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// CRUD for savings goals and goal contributions.
/// Data is stored in MySQL through Entity Framework Core.
/// </summary>
public class SavingsGoalService : ISavingsGoalService
{
    private readonly ApplicationDbContext _db;

    public SavingsGoalService(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<SavingsGoal> GetAll(int userId)
    {
        return _db.SavingsGoals
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Id)
            .ToList();
    }

    public SavingsGoal? Get(int userId, int id)
    {
        return _db.SavingsGoals
            .FirstOrDefault(g =>
                g.UserId == userId &&
                g.Id == id);
    }

    public SavingsGoal Add(SavingsGoal goal)
    {
        // New goals always start with zero saved.
        goal.SavedAmount = 0m;

        _db.SavingsGoals.Add(goal);
        _db.SaveChanges();

        return goal;
    }

    /// <summary>
    /// Updates a goal's name, target amount, deadline and note. The saved
    /// amount and its contribution history are preserved untouched, so the
    /// countdown and savings plan recompute straight from the new values.
    /// </summary>
    public bool Update(int userId, SavingsGoal goal)
    {
        var existing = _db.SavingsGoals
            .FirstOrDefault(g =>
                g.UserId == userId &&
                g.Id == goal.Id);

        if (existing is null)
            return false;

        existing.Name = goal.Name;
        existing.TargetAmount = goal.TargetAmount;
        existing.Deadline = goal.Deadline;
        existing.Note = goal.Note ?? string.Empty;

        _db.SaveChanges();

        return true;
    }

    public bool Delete(int userId, int id)
    {
        var goal = _db.SavingsGoals
            .FirstOrDefault(g =>
                g.UserId == userId &&
                g.Id == id);

        if (goal is null)
            return false;

        // GoalContribution records are configured for cascade delete.
        _db.SavingsGoals.Remove(goal);
        _db.SaveChanges();

        return true;
    }

    public bool AddFunds(
        int userId,
        int goalId,
        decimal amount,
        DateTime date,
        string note)
    {
        var goal = _db.SavingsGoals
            .FirstOrDefault(g =>
                g.UserId == userId &&
                g.Id == goalId);

        if (goal is null)
            return false;

        // Add money to the goal.
        goal.SavedAmount += amount;

        // Record the contribution.
        var contribution = new GoalContribution
        {
            GoalId = goal.Id,
            UserId = userId,
            Amount = amount,
            Date = date,
            Note = note ?? string.Empty
        };

        _db.GoalContributions.Add(contribution);

        _db.SaveChanges();

        return true;
    }

    public List<GoalProgress> GetProgress(int userId)
    {
        return _db.SavingsGoals
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Id)
            .Select(g => new GoalProgress
            {
                Goal = g,

                Percent = g.TargetAmount > 0
                    ? Math.Round(
                        g.SavedAmount / g.TargetAmount * 100,
                        1)
                    : 0,

                Remaining = Math.Max(
                    0,
                    g.TargetAmount - g.SavedAmount)
            })
            .ToList();
    }

    public decimal SavedInMonth(
        int userId,
        int year,
        int month)
    {
        return _db.GoalContributions
            .Where(c =>
                c.UserId == userId &&
                c.Date.Year == year &&
                c.Date.Month == month)
            .Sum(c => c.Amount);
    }

    public decimal SavedInRange(int userId, DateTime from, DateTime to)
    {
        var toExclusive = to.Date.AddDays(1);

        return _db.GoalContributions
            .Where(c =>
                c.UserId == userId &&
                c.Date >= from.Date &&
                c.Date < toExclusive)
            .Sum(c => c.Amount);
    }

    public List<MonthAmount> MonthlyContributions(
        int userId,
        int months)
    {
        var result = new List<MonthAmount>();

        var current = new DateTime(
            DateTime.Now.Year,
            DateTime.Now.Month,
            1);

        for (var i = months - 1; i >= 0; i--)
        {
            var month = current.AddMonths(-i);

            var amount = _db.GoalContributions
                .Where(c =>
                    c.UserId == userId &&
                    c.Date.Year == month.Year &&
                    c.Date.Month == month.Month)
                .Sum(c => c.Amount);

            result.Add(new MonthAmount
            {
                Year = month.Year,
                Month = month.Month,
                Amount = amount
            });
        }

        return result;
    }

    public List<GoalContribution> ContributionsFor(
        int userId,
        int goalId)
    {
        return _db.GoalContributions
            .Where(c =>
                c.UserId == userId &&
                c.GoalId == goalId)
            .OrderByDescending(c => c.Date)
            .ToList();
    }
}