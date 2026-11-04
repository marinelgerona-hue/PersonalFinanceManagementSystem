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

    /// <summary>First date (inclusive) the budget covers.</summary>
    [Required(ErrorMessage = "Please choose a start date.")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    /// <summary>Last date (inclusive) the budget covers. Must be on or
    /// after StartDate (checked in the controller).</summary>
    [Required(ErrorMessage = "Please choose an end date.")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.Today;
}

/// <summary>One budget shown as a choice in the Add Expense form. Carries the
/// date window so the browser can filter options as category/date change.</summary>
public class BudgetOption
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;

    /// <summary>Inclusive first/last date the budget covers.</summary>
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    /// <summary>True when the new expense's category and date match this
    /// budget — it is the automatic deduction target.</summary>
    public bool IsMatch { get; set; }
}

public class BudgetExpenseFormViewModel
{
    [Required]
    public int BudgetId { get; set; }

    [Required(ErrorMessage = "Please enter an amount.")]
    [Range(0.01, 99_999_999, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Please choose a date.")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "Income period to deduct from")]
    public IncomePeriod IncomePool { get; set; } = IncomePeriod.Monthly;

    [StringLength(300)]
    public string? Note { get; set; }
}

public class BudgetExpenseHistoryItem
{
    public Budget Budget { get; set; } = new();
    public Expense Expense { get; set; } = new();
}

public class BudgetListViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Currency { get; set; } = "USD";
    /// <summary>All budgets for the month (used by the summary cards).</summary>
    public List<BudgetStatus> Items { get; set; } = new();

    public BudgetExpenseFormViewModel DailyExpense { get; set; } = new();
    public List<BudgetExpenseHistoryItem> DailyHistory { get; set; } = new();

    /// <summary>The current page of the (searched, sorted) budget table.</summary>
    public PagedList<BudgetStatus> Page { get; set; } = new();
    public ListQuery Query { get; set; } = new();

    public decimal TotalBudget => Items.Sum(i => i.Budget.Limit);
    public decimal TotalSpent => Items.Sum(i => i.Spent);
    public decimal TotalRemaining => TotalBudget - TotalSpent;
    public decimal OverallPercent => TotalBudget > 0 ? Math.Round(TotalSpent / TotalBudget * 100, 1) : 0;
    public int OverCount => Items.Count(i => i.IsOver);
}
