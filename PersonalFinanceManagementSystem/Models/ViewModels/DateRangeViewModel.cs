namespace PersonalFinanceManagementSystem.Models.ViewModels;

/// <summary>
/// Custom date-range report — accurate income, expense, net and savings totals
/// computed strictly from transactions between From and To (inclusive).
/// </summary>
public class DateRangeViewModel
{
    public DateTime From { get; set; } = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    public DateTime To { get; set; } = DateTime.Today;

    public string Currency { get; set; } = "USD";

    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal RemainingBalance => TotalIncome - TotalExpenses;

    public int IncomeCount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Saved { get; set; }

    public decimal AveragePerDay => Days > 0 ? Math.Round(TotalExpenses / Days, 2) : 0;

    public int Days => (To.Date - From.Date).Days + 1;

    public List<CategorySlice> TopCategories { get; set; } = new();
    public List<Income> IncomeItems { get; set; } = new();
    public List<Expense> ExpenseItems { get; set; } = new();

    public string Label => $"{From:MMM d, yyyy} – {To:MMM d, yyyy}";
}
