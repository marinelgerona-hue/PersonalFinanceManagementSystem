using System.ComponentModel.DataAnnotations;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

public class RecurringTransactionFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please choose a type.")]
    public RecurringTransactionType Type { get; set; } = RecurringTransactionType.Expense;

    [Required(ErrorMessage = "Please choose a category or source.")]
    public string Label { get; set; } = string.Empty;

    /// <summary>Custom label typed by the user when Label is "Other". Only read
    /// when Label == "Other" (validated in the controller); empty otherwise.</summary>
    [StringLength(100, ErrorMessage = "Custom label must be 100 characters or fewer.")]
    public string? CustomLabel { get; set; }

    [Required(ErrorMessage = "Please enter an amount.")]
    [Range(0.01, 99_999_999, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Please choose how often this repeats.")]
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;

    [Required(ErrorMessage = "Please choose a start date.")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(300)]
    public string? Note { get; set; }

    /// <summary>Income pool this generates expenses from. Type == Expense only.</summary>
    [Display(Name = "Income period to deduct from")]
    public IncomePeriod IncomePool { get; set; } = IncomePeriod.Monthly;

}

public class RecurringTransactionListViewModel
{
    public PagedList<RecurringTransaction> Page { get; set; } = new();
    public ListQuery Query { get; set; } = new();
    public string Currency { get; set; } = "USD";

    public int ActiveCount { get; set; }
    public int DueSoonCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal MonthlyTotal { get; set; }

}

public class RecurringTransactionDetailsViewModel
{
    public RecurringTransaction Item { get; set; } = new();
    public string Currency { get; set; } = "USD";
    public List<RecentTransaction> History { get; set; } = new();
}
