namespace SmartExpense.Application.Dtos.Analytics;

public class FinancialOverviewDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetBalance { get; set; }
    public decimal SavingsRate { get; set; } // (Income - Expense) / Income * 100

    // Averages
    public decimal AverageDailyIncome { get; set; }
    public decimal AverageDailyExpense { get; set; }

    // Counts
    public int TotalTransactions { get; set; }
    public int IncomeTransactionCount { get; set; }
    public int ExpenseTransactionCount { get; set; }

    // Top Categories
    public List<TopCategoryDto> TopExpenseCategories { get; set; } = new();
    public List<TopCategoryDto> TopIncomeCategories { get; set; } = new();

    // Trends
    public List<SpendingTrendDto> DailyTrend { get; set; } = new();
}