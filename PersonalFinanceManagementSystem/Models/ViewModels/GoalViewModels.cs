using System.ComponentModel.DataAnnotations;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

public class GoalFormViewModel
{
    /// <summary>Goal id — set only when editing an existing goal.</summary>
    public int Id { get; set; }

    [Required(ErrorMessage = "Please give your goal a name.")]
    [StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a target amount.")]
    [Range(1, 99_999_999, ErrorMessage = "Target must be at least 1.")]
    [Display(Name = "Target amount")]
    public decimal TargetAmount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Target date (optional)")]
    public DateTime? Deadline { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}

/// <summary>Inline "add funds" form shown on each goal card.</summary>
public class AddFundsViewModel
{
    public int GoalId { get; set; }
    public string GoalName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an amount.")]
    [Range(0.01, 99_999_999, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [StringLength(200)]
    public string? Note { get; set; }
}

public class GoalListViewModel
{
    /// <summary>All goals (used by the summary cards).</summary>
    public List<GoalProgress> Items { get; set; } = new();

    /// <summary>The current page of the (searched, sorted) goal cards.</summary>
    public PagedList<GoalProgress> Page { get; set; } = new();
    public ListQuery Query { get; set; } = new();

    public string Currency { get; set; } = "USD";
    public decimal TotalSaved => Items.Sum(i => i.Saved);
    public decimal TotalTarget => Items.Sum(i => i.Target);
    public decimal OverallPercent => TotalTarget > 0 ? Math.Round(TotalSaved / TotalTarget * 100, 1) : 0;
    public decimal ThisMonthSaved { get; set; }
    public int TotalCount => Items.Count;
    public int CompletedCount => Items.Count(i => i.Completed);
}
