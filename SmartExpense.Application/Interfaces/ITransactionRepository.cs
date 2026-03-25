using SmartExpense.Core.Entities;
using SmartExpense.Core.Models;

namespace SmartExpense.Application.Interfaces;

public interface ITransactionRepository : IGenericRepository<Transaction>
{
    Task<PagedResult<Transaction>> GetPagedAsync(Guid userId, TransactionQueryParameters parameters);
    Task<List<Transaction>> GetRecentAsync(Guid userId, int count = 10);
    Task<decimal> GetTotalIncomeAsync(Guid userId, DateTime? startDate, DateTime? endDate);
    Task<decimal> GetTotalExpenseAsync(Guid userId, DateTime? startDate, DateTime? endDate);
    Task<int> GetTransactionCountAsync(Guid userId, DateTime? startDate, DateTime? endDate);
    Task<Transaction?> GetByIdForUserAsync(int id, Guid userId);

    /// <summary>
    /// Checks whether a transaction has already been generated for a given recurring
    /// template on a specific date. Uses a FK lookup for reliable, index-backed deduplication.
    /// </summary>
    /// <param name="recurringTransactionId">The ID of the recurring transaction template.</param>
    /// <param name="date">The due date to check.</param>
    /// <returns><c>true</c> if a generated transaction already exists for that template and date.</returns>
    Task<bool> ExistsForRecurringOnDateAsync(int recurringTransactionId, DateTime date);
}