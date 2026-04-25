namespace SmartExpense.Application.Dtos.Budget;

public class BudgetSummaryDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalBudgeted { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalRemaining { get; set; }
    public decimal PercentageUsed { get; set; }
    public int TotalBudgets { get; set; }
    public int BudgetsExceeded { get; set; }
    public int BudgetsApproaching { get; set; }
    public List<BudgetReadDto> Budgets { get; set; } = new();
}