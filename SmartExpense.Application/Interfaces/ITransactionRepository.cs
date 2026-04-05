using SmartExpense.Core.Entities;
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
    Task<bool> ExistsForRecurringOnDateAsync(int recurringTransactionId, DateTime date, CancellationToken cancellationToken = default);
    Task<decimal> GetActualSpentAsync(
        Guid userId,
        int categoryId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}