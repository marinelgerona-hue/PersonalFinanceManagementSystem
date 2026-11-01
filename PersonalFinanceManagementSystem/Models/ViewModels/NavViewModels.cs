namespace PersonalFinanceManagementSystem.Models.ViewModels;

/// <summary>Live data shown in the app shell (sidebar badges, budget meter, notifications).</summary>
public class NavState
{
    public string MonthLabel { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal MonthSpent { get; set; }
    public decimal MonthBudgeted { get; set; }
    public int BudgetsOver { get; set; }
    public List<NavAlert> Alerts { get; set; } = new();

    public decimal BudgetUsedPercent => MonthBudgeted > 0 ? Math.Round(MonthSpent / MonthBudgeted * 100, 1) : 0;
}

/// <summary>One notification in the top-bar bell menu.</summary>
public class NavAlert
{
    /// <summary>"danger", "warn" or "info" — drives the icon color.</summary>
    public string Level { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Controller { get; set; } = "Dashboard";
}
