using SmartExpense.Core.Enums;
using SmartExpense.Core.Interfaces;

namespace SmartExpense.Core.Entities;

public class RecurringTransaction : IAuditable, IEntity, IUserOwnedEntity
{
    public int CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType TransactionType { get; set; }
    public RecurrenceFrequency Frequency { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastGeneratedDate { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public int Id { get; set; }
    public Guid UserId { get; set; }
}