using SmartExpense.Core.Enums;

namespace SmartExpense.Application.Dtos.Transaction;

public class TransactionCreateDto
{
    public int CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }
}