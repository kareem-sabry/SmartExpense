namespace SmartExpense.Application.Dtos.RecurringTransaction;

public class GenerateTransactionsResultDto
{
    public int TransactionsGenerated { get; set; }
    public List<GeneratedTransactionInfo> GeneratedTransactions { get; set; } = new();
}

public class GeneratedTransactionInfo
{
    public int RecurringTransactionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
}