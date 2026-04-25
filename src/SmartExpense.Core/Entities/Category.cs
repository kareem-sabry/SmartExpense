using SmartExpense.Core.Interfaces;

namespace SmartExpense.Core.Entities;

public class Category : IAuditable, IEntity
{
    public Guid? UserId { get; set; } // Nullable for system categories
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsSystemCategory { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public int Id { get; set; }
}