using System.ComponentModel.DataAnnotations;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

public class BudgetFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please choose a category.")]
    public string Category { get; set; } = string.Empty;

    /// <summary>Custom category typed by the user when Category is
    /// "Other". Only read when Category == "Other" (validated in the
    /// controller); empty otherwise.</summary>
    [StringLength(100, ErrorMessage = "Custom category must be 100 characters or fewer.")]
    public string? CustomCategory { get; set; }

    [Required(ErrorMessage = "Please enter a limit.")]
    [Range(0.01, 99_999_999, ErrorMessage = "Limit must be greater than 0.")]
    [Display(Name = "Limit")]
    public decimal Limit { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; } = DateTime.Now.Year;

    [Range(1, 12)]
    public int Month { get; set; } = DateTime.Now.Month;

    /// <summary>Budget period: daily, weekly, monthly or yearly. Defaults to
    /// monthly so existing budgets keep the behaviour they always had.</summary>
    public BudgetPeriod Period { get; set; } = BudgetPeriod.Monthly;

    /// <summary>Anchor day for daily and weekly budgets (1-31). For weekly
    /// budgets, Year/Month/Day is the day the budget week starts. Ignored
    /// for monthly and yearly periods.</summary>
    [Range(1, 31)]
    public int Day { get; set; } = 1;

    /// <summary>Convenience picker for daily and weekly budgets: the date
    /// the budget covers (daily) or the day its week starts (weekly).
    /// Fills Year/Month/Day, never stored itself.</summary>
    [DataType(DataType.Date)]
    public DateTime? AnchorDate { get; set; }

    /// <summary>Labels for the period dropdown (shared with the expense form).</summary>
    public static List<(BudgetPeriod Period, string Label)> PeriodLabels { get; } = new()
    {
        (BudgetPeriod.Monthly, "Monthly"),
        (BudgetPeriod.Daily, "Daily"),
        (BudgetPeriod.Weekly, "Weekly"),
        (BudgetPeriod.Yearly, "Yearly")
    };
}

/// <summary>One budget shown as a choice in the Add Expense form. Carries the
/// period window so the browser can filter options as category/date change.</summary>
public class BudgetOption
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public BudgetPeriod Period { get; set; }

    /// <summary>Inclusive first/last date the budget covers.</summary>
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    /// <summary>True when the new expense's category and date match this
    /// budget — it is the automatic deduction target.</summary>
    public bool IsMatch { get; set; }
}

public class BudgetListViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Currency { get; set; } = "USD";
    /// <summary>All budgets for the month (used by the summary cards).</summary>
    public List<BudgetStatus> Items { get; set; } = new();

    /// <summary>The current page of the (searched, sorted) budget table.</summary>
    public PagedList<BudgetStatus> Page { get; set; } = new();
    public ListQuery Query { get; set; } = new();

    public decimal TotalBudget => Items.Sum(i => i.Budget.Limit);
    public decimal TotalSpent => Items.Sum(i => i.Spent);
    public decimal TotalRemaining => TotalBudget - TotalSpent;
    public decimal OverallPercent => TotalBudget > 0 ? Math.Round(TotalSpent / TotalBudget * 100, 1) : 0;
    public int OverCount => Items.Count(i => i.IsOver);
}
