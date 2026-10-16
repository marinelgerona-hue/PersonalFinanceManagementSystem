using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

/// <summary>Everything shown on the dashboard — all values computed from live in-memory data.</summary>
public class DashboardViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";

    // Top stat cards
    public decimal Balance { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal TotalSaved { get; set; }
    public decimal SavingsTarget { get; set; }
    public decimal SavingsPercent { get; set; }

    // Widgets
    public List<CategorySlice> Spending { get; set; } = new();
    public List<MonthPoint> Trend { get; set; } = new();
    public List<BudgetStatus> Budgets { get; set; } = new();
    public List<GoalProgress> Goals { get; set; } = new();
    public List<RecentTransaction> Recent { get; set; } = new();

    public decimal TrendMax => Trend.Count == 0 ? 0 : Math.Max(Trend.Max(t => t.Income), Trend.Max(t => t.Expenses));
    public decimal SpendingTotal => Spending.Sum(s => s.Amount);
}
