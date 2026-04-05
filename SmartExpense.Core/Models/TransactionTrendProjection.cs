using SmartExpense.Core.Enums;

namespace SmartExpense.Core.Models;

/// <summary>
///     Lightweight projection for spending-trend queries.
///     Only the three columns needed for daily/weekly/monthly grouping are fetched —
///     no entity is materialised, no Category navigation is loaded.
/// </summary>
public record TransactionTrendProjection(
    DateTime TransactionDate,
    TransactionType TransactionType,
    decimal Amount);