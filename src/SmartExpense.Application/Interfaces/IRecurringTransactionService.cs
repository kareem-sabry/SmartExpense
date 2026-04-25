using SmartExpense.Application.Dtos.RecurringTransaction;

namespace SmartExpense.Application.Interfaces;

public interface IRecurringTransactionService
{
    Task<List<RecurringTransactionReadDto>> GetAllAsync(Guid userId, bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<RecurringTransactionReadDto> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<RecurringTransactionReadDto> CreateAsync(RecurringTransactionCreateDto dto, Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecurringTransactionReadDto> UpdateAsync(int id, RecurringTransactionUpdateDto dto, Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<RecurringTransactionReadDto> ToggleActiveAsync(int id, Guid userId,
        CancellationToken cancellationToken = default);

    Task<GenerateTransactionsResultDto> GenerateTransactionsAsync(Guid userId,
        CancellationToken cancellationToken = default);

    Task<GenerateTransactionsResultDto> GenerateForRecurringTransactionAsync(int recurringId, Guid userId,
        CancellationToken cancellationToken = default);
}