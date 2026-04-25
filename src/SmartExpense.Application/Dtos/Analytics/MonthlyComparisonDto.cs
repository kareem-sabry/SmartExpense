namespace SmartExpense.Application.Dtos.Analytics;

public class MonthlyComparisonDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetBalance { get; set; }
    public decimal IncomeChange { get; set; }
    public decimal ExpenseChange { get; set; }
    public int TransactionCount { get; set; }
}