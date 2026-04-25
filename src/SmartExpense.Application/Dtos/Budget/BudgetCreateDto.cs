namespace SmartExpense.Application.Dtos.Budget;

public class BudgetCreateDto
{
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
}