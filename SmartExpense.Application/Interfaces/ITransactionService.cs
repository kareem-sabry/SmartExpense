using SmartExpense.Application.Dtos.Transaction;
using SmartExpense.Core.Models;

namespace SmartExpense.Application.Interfaces;

public interface ITransactionService
{
    Task<PagedResult<TransactionReadDto>> GetPagedAsync(Guid userId, TransactionQueryParameters parameters);
    Task<TransactionReadDto> GetByIdAsync(int id, Guid userId);
    Task<List<TransactionReadDto>> GetRecentAsync(Guid userId, int count = 10);
    Task<TransactionSummaryDto> GetSummaryAsync(Guid userId, DateTime? startDate, DateTime? endDate);
    Task<TransactionReadDto> CreateAsync(TransactionCreateDto dto, Guid userId);
    Task<TransactionReadDto> UpdateAsync(int id, TransactionUpdateDto dto, Guid userId);
    Task DeleteAsync(int id, Guid userId);
    Task<string> ExportToCsvAsync(Guid userId, DateTime startDate, DateTime endDate);
}