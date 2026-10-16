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

    public string FirstName { get; set; } = string.Empty;

    public decimal MonthlyNet => MonthlyIncome - MonthlyExpenses;

    /// <summary>Share of this month's income that wasn't spent.</summary>
    public decimal SavingsRate => MonthlyIncome > 0 ? Math.Round(MonthlyNet / MonthlyIncome * 100, 1) : 0;

    public MonthPoint? PreviousMonth => Trend.Count > 1 ? Trend[^2] : null;

    /// <summary>% change in income vs last month (null when last month had none).</summary>
    public decimal? IncomeChange => Change(MonthlyIncome, PreviousMonth?.Income);

    /// <summary>% change in expenses vs last month (null when last month had none).</summary>
    public decimal? ExpenseChange => Change(MonthlyExpenses, PreviousMonth?.Expenses);

    public decimal TrendIncomeTotal => Trend.Sum(t => t.Income);
    public decimal TrendExpenseTotal => Trend.Sum(t => t.Expenses);

    public int BudgetsOver => Budgets.Count(b => b.IsOver);

    private static decimal? Change(decimal current, decimal? previous)
        => previous is > 0 ? Math.Round((current - previous.Value) / previous.Value * 100, 1) : null;

    public decimal TrendMax => Trend.Count == 0 ? 0 : Math.Max(Trend.Max(t => t.Income), Trend.Max(t => t.Expenses));
    public decimal SpendingTotal => Spending.Sum(s => s.Amount);
}
