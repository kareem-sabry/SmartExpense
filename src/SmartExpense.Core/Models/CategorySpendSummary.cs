namespace SmartExpense.Core.Models;

/// <summary>
///     Lightweight result returned by the repository for category-grouped spend queries.
///     The SQL GROUP BY + SUM runs in the database; this record carries the result to the service.
/// </summary>
public record CategorySpendSummary(
    int CategoryId,
    string CategoryName,
    string? CategoryIcon,
    string? CategoryColor,
    decimal TotalAmount,
    int TransactionCount);