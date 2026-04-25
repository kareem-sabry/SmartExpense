using SmartExpense.Core.Enums;
using SmartExpense.Core.Interfaces;

namespace SmartExpense.Core.Entities;

public class Transaction : IAuditable, IEntity, IUserOwnedEntity
{
    public int CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    ///     Foreign key to the recurring transaction template that auto-generated this entry.
    ///     <c>null</c> for transactions created manually by the user.
    /// </summary>
    public int? RecurringTransactionId { get; set; }

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;

    /// <summary>
    ///     Navigation property to the recurring template that generated this transaction.
    ///     <c>null</c> for manually created transactions.
    /// </summary>
    public RecurringTransaction? RecurringTransaction { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public int Id { get; set; }
    public Guid UserId { get; set; }
}