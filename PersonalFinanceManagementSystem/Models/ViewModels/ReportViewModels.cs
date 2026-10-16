using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

/// <summary>Monthly Summary report — income, expenses, net and savings for one month.</summary>
public class MonthlySummaryViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";

    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal Net => TotalIncome - TotalExpenses;
    public decimal Saved { get; set; }
    public int IncomeCount { get; set; }
    public int ExpenseCount { get; set; }
    public int DaysElapsed { get; set; }
    public decimal AveragePerDay => DaysElapsed > 0 ? Math.Round(TotalExpenses / DaysElapsed, 2) : 0;

    public Expense? LargestExpense { get; set; }
    public Income? LargestIncome { get; set; }
    public List<CategorySlice> TopCategories { get; set; } = new();
    public List<BudgetStatus> Budgets { get; set; } = new();
}

/// <summary>Expense Analysis report — category breakdown for one month.</summary>
public class ExpenseAnalysisViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";

    public decimal Total { get; set; }
    public decimal PreviousMonthTotal { get; set; }
    public decimal ChangePercent => PreviousMonthTotal > 0 ? Math.Round((Total - PreviousMonthTotal) / PreviousMonthTotal * 100, 1) : 0;
    public decimal AveragePerDay { get; set; }
    public Expense? LargestExpense { get; set; }
    public List<CategorySlice> Categories { get; set; } = new();
}

/// <summary>Savings Report — goal progress and monthly contributions.</summary>
public class SavingsReportViewModel
{
    public string Currency { get; set; } = "USD";
    public decimal TotalSaved { get; set; }
    public decimal TotalTarget { get; set; }
    public decimal TotalRemaining => TotalTarget - TotalSaved;
    public decimal OverallPercent => TotalTarget > 0 ? Math.Round(TotalSaved / TotalTarget * 100, 1) : 0;
    public decimal ThisMonthSaved { get; set; }
    public int GoalsCompleted { get; set; }
    public List<GoalProgress> Goals { get; set; } = new();
    public List<MonthAmount> MonthlyContributions { get; set; } = new();
    public decimal MaxContribution => MonthlyContributions.Count == 0 ? 0 : MonthlyContributions.Max(m => m.Amount);
}

/// <summary>Budget Report — budgets vs actual spending for one month.</summary>
public class BudgetReportViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";

    public List<BudgetStatus> Budgets { get; set; } = new();
    public decimal TotalBudget => Budgets.Sum(b => b.Budget.Limit);
    public decimal TotalSpent => Budgets.Sum(b => b.Spent);
    public decimal TotalRemaining => TotalBudget - TotalSpent;
    public decimal OverallPercent => TotalBudget > 0 ? Math.Round(TotalSpent / TotalBudget * 100, 1) : 0;
    public int OverCount => Budgets.Count(b => b.IsOver);
    public int NearCount => Budgets.Count(b => b.IsNear);
}

/// <summary>Income Report — totals, sources and 12-month trend.</summary>
public class IncomeReportViewModel
{
    public string Currency { get; set; } = "USD";
    public decimal Total { get; set; }
    public decimal ThisMonth { get; set; }
    public decimal AverageMonthly { get; set; }
    public MonthPoint? BestMonth { get; set; }
    public List<SourceSlice> Sources { get; set; } = new();
    public List<MonthPoint> Trend { get; set; } = new();
    public int RecordCount { get; set; }
    public decimal TrendMax => Trend.Count == 0 ? 0 : Trend.Max(t => t.Income);
}

/// <summary>One row of the income-by-source table.</summary>
public class SourceSlice
{
    public string Source { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Percent { get; set; }
    public int Count { get; set; }
    public decimal Average => Count > 0 ? Math.Round(Amount / Count, 2) : 0;
}

/// <summary>Annual Summary report — month-by-month rows for one year.</summary>
public class AnnualSummaryViewModel
{
    public int Year { get; set; }
    public string Currency { get; set; } = "USD";
    public List<AnnualMonthRow> Months { get; set; } = new();
    public decimal TotalIncome => Months.Sum(m => m.Income);
    public decimal TotalExpenses => Months.Sum(m => m.Expenses);
    public decimal TotalNet => TotalIncome - TotalExpenses;
    public decimal TotalSaved => Months.Sum(m => m.Saved);
    public decimal AverageMonthlyIncome => Months.Count(m => m.Income > 0) > 0 ? Math.Round(TotalIncome / Months.Count(m => m.Income > 0), 2) : 0;
    public decimal AverageMonthlyExpenses => Months.Count(m => m.Expenses > 0) > 0 ? Math.Round(TotalExpenses / Months.Count(m => m.Expenses > 0), 2) : 0;
    public AnnualMonthRow? BestMonth => Months.OrderByDescending(m => m.Net).FirstOrDefault();
    public List<CategorySlice> TopCategories { get; set; } = new();
    public decimal TrendMax => Months.Count == 0 ? 0 : Math.Max(Months.Max(m => m.Income), Months.Max(m => m.Expenses));
}
