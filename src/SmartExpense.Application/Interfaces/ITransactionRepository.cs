using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Models;


namespace SmartExpense.Application.Interfaces;

public interface ITransactionRepository : IGenericRepository<Transaction>
{
    Task<PagedResult<Transaction>> GetPagedAsync(Guid userId, TransactionQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<List<Transaction>> GetRecentAsync(Guid userId, int count = 10, CancellationToken cancellationToken = default);

    Task<decimal> GetTotalIncomeAsync(Guid userId, DateTime? startDate, DateTime? endDate,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalExpenseAsync(Guid userId, DateTime? startDate, DateTime? endDate,
        CancellationToken cancellationToken = default);

    Task<int> GetTransactionCountAsync(Guid userId, DateTime? startDate, DateTime? endDate,
        CancellationToken cancellationToken = default);

    Task<Transaction?> GetByIdForUserAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForRecurringOnDateAsync(int recurringTransactionId, DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the total amount spent in a specific category during the given date range.
    ///     Executes a single SQL SUM — never loads rows into memory.
    /// </summary>
    Task<decimal> GetActualSpentAsync(
        Guid userId,
        int categoryId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the transaction count for a specific type within an optional date range.
    ///     SQL COUNT — no rows loaded into memory.
    /// </summary>
    Task<int> GetCountByTypeAsync(
        Guid userId,
        TransactionType type,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a SQL GROUP BY summary — one row per category with total amount and count.
    ///     No full Transaction entities are loaded into memory.
    /// </summary>
    Task<List<CategorySpendSummary>> GetCategorySpendSummaryAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        TransactionType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a minimal (Date, Type, Amount) projection for trend calculations.
    ///     Replaces loading full Transaction entities when only three scalar columns are needed.
    /// </summary>
    Task<List<TransactionTrendProjection>> GetTrendProjectionsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}