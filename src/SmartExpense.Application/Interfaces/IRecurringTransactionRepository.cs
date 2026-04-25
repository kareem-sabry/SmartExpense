using SmartExpense.Core.Entities;

namespace SmartExpense.Application.Interfaces;

public interface IRecurringTransactionRepository : IGenericRepository<RecurringTransaction>
{
    Task<RecurringTransaction?> GetByIdForUserAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<List<RecurringTransaction>> GetAllForUserAsync(Guid userId, bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<List<RecurringTransaction>> GetDueForGenerationAsync(DateTime asOfDate,
        CancellationToken cancellationToken = default);
}