using SmartExpense.Core.Enums;

namespace SmartExpense.Application.Dtos.RecurringTransaction;

public class RecurringTransactionUpdateDto
{
   
    public int CategoryId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public TransactionType TransactionType { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Notes { get; set; }
}