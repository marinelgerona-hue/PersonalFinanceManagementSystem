using System.ComponentModel.DataAnnotations;
using PersonalFinanceManagementSystem.Models;

namespace PersonalFinanceManagementSystem.Models.ViewModels;

public class IncomeFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please choose an income source.")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Custom income source typed by the user when Source is
    /// "Other". Only read when Source == "Other" (validated in the controller);
    /// empty otherwise, so it never blocks a normal source choice.</summary>
    [StringLength(100, ErrorMessage = "Custom source must be 100 characters or fewer.")]
    public string? CustomSource { get; set; }

    [Required(ErrorMessage = "Please enter an amount.")]
    [Range(0.01, 99_999_999, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Please choose an income period.")]
    [Display(Name = "Income period")]
    public IncomePeriod Period { get; set; } = IncomePeriod.Monthly;

    [Required(ErrorMessage = "Please choose a date.")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [StringLength(300)]
    public string? Note { get; set; }
}

public class IncomeListViewModel
{
    public List<Income> Items { get; set; } = new();
    public string Currency { get; set; } = "USD";
    public decimal MonthTotal { get; set; }
    public decimal Total { get; set; }
    public int ThisMonthCount { get; set; }

    /// <summary>Selected period filter for the table (null = all periods).</summary>
    public IncomePeriod? PeriodFilter { get; set; }

    /// <summary></summary>
    public List<PeriodSlice> PeriodBreakdown { get; set; } = new();
}

/// <summary>One row of the period breakdown (totals per Daily/Weekly/Monthly/Yearly).</summary>
public class PeriodSlice
{
    public IncomePeriod Period { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Tint { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
    public string Color { get; set; } = string.Empty;
    public decimal Percent { get; set; }
}
