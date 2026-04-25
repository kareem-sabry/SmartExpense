namespace SmartExpense.Core.Interfaces;

public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    string? UpdatedBy { get; set; }
}