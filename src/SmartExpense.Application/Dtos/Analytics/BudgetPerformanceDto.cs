namespace SmartExpense.Application.Dtos.Analytics;

public class BudgetPerformanceDto
{
    public int BudgetId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryIcon { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal ActualSpent { get; set; }
    public decimal Remaining { get; set; }
    public decimal PercentageUsed { get; set; }
    public string Status { get; set; } = string.Empty; // "Under", "Approaching", "Exceeded"
    public bool IsOnTrack { get; set; }
}