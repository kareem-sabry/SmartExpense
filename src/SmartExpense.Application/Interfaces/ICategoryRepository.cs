using SmartExpense.Core.Entities;

namespace SmartExpense.Application.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<List<Category>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdForUserAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> CategoryNameExistsAsync(Guid userId, string name, int? excludeId = null,
        CancellationToken cancellationToken = default);
}