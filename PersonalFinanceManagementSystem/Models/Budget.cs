namespace PersonalFinanceManagementSystem.Models;

/// <summary>
/// How long a budget lasts. Monthly = 0 so existing budgets (created before
/// periods existed) keep working: EF stores this as an int and every legacy
/// row reads back as Monthly.
/// </summary>
public enum BudgetPeriod
{
    Monthly = 0,
    Daily = 1,
    Weekly = 2,
    Yearly = 3
}

/// <summary>
/// A spending limit for one category, covering a budget period
/// (daily, weekly, monthly or yearly). Year/Month anchor the period and
/// Day anchors daily/weekly budgets.
/// </summary>
public class Budget
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Category { get; set; } = string.Empty;

    /// <summary>Spending limit for this category over the budget period.</summary>
    public decimal Limit { get; set; }

    public int Year { get; set; }

    /// <summary>Calendar month the period is anchored to (1-12). Ignored for yearly budgets.</summary>
    public int Month { get; set; }

    /// <summary>Budget period: daily, weekly, monthly or yearly. Legacy rows are Monthly.</summary>
    public BudgetPeriod Period { get; set; } = BudgetPeriod.Monthly;

    /// <summary>
    /// Day of month used for Daily and Weekly budgets (1-31).
    /// For a weekly budget, Year/Month/Day is the day the budget week starts.
    /// Ignored for monthly and yearly budgets.
    /// </summary>
    public int Day { get; set; } = 1;

    // ------------------------------------------------------- period window

    /// <summary>First date (inclusive) that this budget covers.</summary>
    public DateTime PeriodStart => Period switch
    {
        BudgetPeriod.Daily => new DateTime(Year, Month, Day),
        BudgetPeriod.Weekly => new DateTime(Year, Month, Day),
        BudgetPeriod.Yearly => new DateTime(Year, 1, 1),
        _ => new DateTime(Year, Month, 1) // Monthly
    };

    /// <summary>Last date (inclusive) that this budget covers.</summary>
    public DateTime PeriodEnd => Period switch
    {
        BudgetPeriod.Daily => new DateTime(Year, Month, Day),
        BudgetPeriod.Weekly => new DateTime(Year, Month, Day).AddDays(6),
        BudgetPeriod.Yearly => new DateTime(Year, 12, 31),
        _ => new DateTime(Year, Month, DateTime.DaysInMonth(Year, Month)) // Monthly
    };

    /// <summary>True when the budget period covers the given date.</summary>
    public bool Covers(DateTime date) =>
        date.Date >= PeriodStart && date.Date <= PeriodEnd;
}
