namespace PersonalFinanceManagementSystem.Models;

/// <summary>
/// A spending limit for one category, covering a Start Date to End Date
/// schedule (both inclusive). An expense is deducted from the budget when
/// its category matches and its date falls inside this window.
/// </summary>
public class Budget
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Category { get; set; } = string.Empty;

    /// <summary>Spending limit for this category over the budget period.</summary>
    public decimal Limit { get; set; }

    /// <summary>First date (inclusive) that this budget covers.</summary>
    public DateTime StartDate { get; set; } = DateTime.Today;

    /// <summary>Last date (inclusive) that this budget covers.</summary>
    public DateTime EndDate { get; set; } = DateTime.Today;

    // ------------------------------------------------------- period window

    /// <summary>True when the budget period covers the given date.</summary>
    public bool Covers(DateTime date) =>
        date.Date >= StartDate.Date && date.Date <= EndDate.Date;
}
