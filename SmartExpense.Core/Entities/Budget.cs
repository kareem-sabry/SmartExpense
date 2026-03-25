using SmartExpense.Core.Interfaces;

namespace SmartExpense.Core.Entities;

public class Budget : IAuditable, IEntity, IUserOwnedEntity
{
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public int Id { get; set; }
    public Guid UserId { get; set; }
}