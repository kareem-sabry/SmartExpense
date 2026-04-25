namespace SmartExpense.Application.Dtos.Analytics;

public class SpendingTrendDto
{
    public DateTime Date { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetBalance { get; set; }
    public int TransactionCount { get; set; }
}