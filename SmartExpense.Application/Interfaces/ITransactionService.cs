using SmartExpense.Application.Dtos.Transaction;
using SmartExpense.Core.Models;

namespace SmartExpense.Application.Interfaces;

public interface ITransactionService
{
    Task<PagedResult<TransactionReadDto>> GetPagedAsync(Guid userId, TransactionQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<TransactionReadDto> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<List<TransactionReadDto>> GetRecentAsync(Guid userId, int count = 10,
        CancellationToken cancellationToken = default);

    Task<TransactionSummaryDto> GetSummaryAsync(Guid userId, DateTime? startDate, DateTime? endDate,
        CancellationToken cancellationToken = default);

    Task<TransactionReadDto> CreateAsync(TransactionCreateDto dto, Guid userId,
        CancellationToken cancellationToken = default);

    Task<TransactionReadDto> UpdateAsync(int id, TransactionUpdateDto dto, Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<string> ExportToCsvAsync(Guid userId, DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default);
}