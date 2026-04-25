using SmartExpense.Application.Dtos.Category;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Exceptions;

namespace SmartExpense.Infrastructure.Services;

/// <summary>
///     Manages user-defined and system categories used to classify transactions and budgets.
///     System categories are read-only and cannot be modified or deleted by users.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initialises a new instance of <see cref="CategoryService" />.</summary>
    /// <param name="unitOfWork">Unit of Work providing access to all repositories.</param>
    public CategoryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    ///     Returns all categories visible to the user: system categories (shared by all users)
    ///     and the user's own custom categories, ordered with system categories first.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    public async Task<List<CategoryReadDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var categories = await _unitOfWork.Categories.GetAllForUserAsync(userId, cancellationToken);

        return categories.Select(c => new CategoryReadDto
        {
            Id = c.Id,
            Name = c.Name,
            Icon = c.Icon,
            Color = c.Color,
            IsSystemCategory = c.IsSystemCategory,
            IsActive = c.IsActive
        }).ToList();
    }

    /// <summary>
    ///     Returns a single category by ID. Accessible if the category is a system category
    ///     or belongs to the authenticated user.
    /// </summary>
    /// <param name="id">The category ID.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="NotFoundException">Thrown when the category does not exist or is not accessible.</exception>
    public async Task<CategoryReadDto> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdForUserAsync(id, userId, cancellationToken);

        if (category == null)
            throw new NotFoundException("Category", id);

        return new CategoryReadDto
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            Color = category.Color,
            IsSystemCategory = category.IsSystemCategory,
            IsActive = category.IsActive
        };
    }

    /// <summary>
    ///     Creates a new custom category for the authenticated user.
    ///     Category names must be unique per user (case-insensitive).
    /// </summary>
    /// <param name="dto">The category creation payload.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="ConflictException">Thrown when a category with the same name already exists for this user.</exception>
    public async Task<CategoryReadDto> CreateAsync(CategoryCreateDto dto, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var exists =
            await _unitOfWork.Categories.CategoryNameExistsAsync(userId, dto.Name,
                cancellationToken: cancellationToken);

        if (exists)
            throw new ConflictException("Category with this name already exists");


        var category = new Category
        {
            UserId = userId,
            Name = dto.Name,
            Icon = dto.Icon,
            Color = dto.Color,
            IsSystemCategory = false,
            IsActive = true
        };

        await _unitOfWork.Categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CategoryReadDto
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            Color = category.Color,
            IsSystemCategory = category.IsSystemCategory,
            IsActive = category.IsActive
        };
    }

    /// <summary>
    ///     Updates a custom category. System categories cannot be modified.
    ///     Category names must remain unique per user (case-insensitive).
    /// </summary>
    /// <param name="id">The category ID to update.</param>
    /// <param name="dto">The update payload.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="NotFoundException">Thrown when the category does not exist or is not accessible.</exception>
    /// <exception cref="ForbiddenException">Thrown when attempting to update a system category.</exception>
    /// <exception cref="ConflictException">Thrown when another category with the same name already exists.</exception>
    public async Task<CategoryReadDto> UpdateAsync(int id, CategoryUpdateDto dto, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdForUserAsync(id, userId, cancellationToken);

        if (category == null)
            throw new NotFoundException("Category", id);

        if (category.IsSystemCategory)
            throw new ForbiddenException("Cannot update system categories");

        var nameExists = await _unitOfWork.Categories.CategoryNameExistsAsync(userId, dto.Name, id, cancellationToken);

        if (nameExists)
            throw new ConflictException("Category with this name already exists");

        category.Name = dto.Name;
        category.Icon = dto.Icon;
        category.Color = dto.Color;
        category.IsActive = dto.IsActive;

        await _unitOfWork.Categories.UpdateAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CategoryReadDto
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            Color = category.Color,
            IsSystemCategory = category.IsSystemCategory,
            IsActive = category.IsActive
        };
    }

    /// <summary>
    ///     Permanently deletes a custom category. System categories cannot be deleted.
    /// </summary>
    /// <param name="id">The category ID to delete.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="NotFoundException">Thrown when the category does not exist or is not accessible.</exception>
    /// <exception cref="ForbiddenException">Thrown when attempting to delete a system category.</exception>
    public async Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var category = await _unitOfWork.Categories.GetByIdForUserAsync(id, userId, cancellationToken);

        if (category == null)
            throw new NotFoundException("Category", id);

        if (category.IsSystemCategory)
            throw new ForbiddenException("Cannot delete system categories");

        await _unitOfWork.Categories.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}