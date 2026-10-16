namespace PersonalFinanceManagementSystem.Models;

/// <summary>How often the income is received: daily, weekly, monthly or yearly.</summary>
public enum IncomePeriod
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Yearly = 3
}

/// <summary>An income record (source, amount, date, period, note).</summary>
public class Income
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Source { get; set; } = string.Empty;

    /// <summary>Amount received for the chosen period (one pay, not annualized).</summary>
    public decimal Amount { get; set; }

    /// <summary>Income period: daily, weekly, monthly or yearly. Existing rows map to Monthly.</summary>
    public IncomePeriod Period { get; set; } = IncomePeriod.Monthly;

    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
}
