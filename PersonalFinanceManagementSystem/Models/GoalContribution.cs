namespace PersonalFinanceManagementSystem.Models;

/// <summary>A single "add funds" contribution made to a savings goal.</summary>
public class GoalContribution
{
    public int Id { get; set; }
    public int GoalId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
}
