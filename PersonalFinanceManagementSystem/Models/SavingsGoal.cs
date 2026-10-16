namespace PersonalFinanceManagementSystem.Models;

/// <summary>A savings goal with target amount and money set aside so far.</summary>
public class SavingsGoal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal SavedAmount { get; set; }
    public DateTime? Deadline { get; set; }
    public string Note { get; set; } = string.Empty;
}
