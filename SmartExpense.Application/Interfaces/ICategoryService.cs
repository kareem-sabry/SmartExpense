using SmartExpense.Application.Dtos.Category;

namespace SmartExpense.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryReadDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CategoryReadDto> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    Task<CategoryReadDto> CreateAsync(CategoryCreateDto dto, Guid userId,
        CancellationToken cancellationToken = default);

    Task<CategoryReadDto> UpdateAsync(int id, CategoryUpdateDto dto, Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
}