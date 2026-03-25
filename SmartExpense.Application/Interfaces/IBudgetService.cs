using SmartExpense.Application.Dtos.Budget;

namespace SmartExpense.Application.Interfaces;

public interface IBudgetService
{
    Task<List<BudgetReadDto>> GetAllAsync(Guid userId, int? month, int? year,
        CancellationToken cancellationToken = default);

    Task<BudgetReadDto> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<BudgetSummaryDto> GetSummaryAsync(Guid userId, int month, int year,
        CancellationToken cancellationToken = default);

    Task<BudgetReadDto> CreateAsync(BudgetCreateDto dto, Guid userId, CancellationToken cancellationToken = default);

    Task<BudgetReadDto> UpdateAsync(int id, BudgetUpdateDto dto, Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
}