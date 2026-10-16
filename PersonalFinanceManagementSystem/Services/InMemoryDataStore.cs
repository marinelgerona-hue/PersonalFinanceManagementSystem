using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Services;

/// <summary>
/// Central in-memory data store (no database). One singleton instance holds every
/// list used by the app. All access goes through a shared lock so concurrent
/// requests can never corrupt the collections.
/// </summary>
public class InMemoryDataStore
{
    private readonly object _lock = new();
    private int _lastId;

    public List<User> Users { get; } = new();
    public List<Income> Incomes { get; } = new();
    public List<Expense> Expenses { get; } = new();
    public List<Budget> Budgets { get; } = new();
    public List<SavingsGoal> Goals { get; } = new();
    public List<GoalContribution> Contributions { get; } = new();

    /// <summary>Lock used to guard every collection in the store.</summary>
    public object SyncRoot => _lock;

    public int NextId()
    {
        lock (_lock) { return ++_lastId; }
    }

    public InMemoryDataStore()
    {
        // Seed a demo account with a year of data so the dashboard and every
        // report have something meaningful to show on first run.
        DemoSeeder.Seed(this);
    }
}
