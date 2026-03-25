using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Infrastructure.Data;

namespace SmartExpense.Infrastructure.Repositories;

/// <summary>
/// Provides data access operations for <see cref="Category"/> entities.
/// Includes both user-owned categories and shared system categories.
/// Read-only queries use <c>AsNoTracking()</c> for reduced memory overhead.
/// </summary>
public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    /// <summary>Initialises a new instance of <see cref="CategoryRepository"/>.</summary>
    public CategoryRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Returns all categories accessible to the given user: system categories (shared by all)
    /// and the user's own custom categories. System categories appear first.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    public async Task<List<Category>> GetAllForUserAsync(Guid userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => c.IsSystemCategory || c.UserId == userId)
            .OrderBy(c => c.IsSystemCategory ? 0 : 1) // System categories first
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Returns a single category accessible to the given user by ID.
    /// Accessible if the category is a system category or owned by the user.
    /// Returns <c>null</c> if not found or not accessible.
    /// </summary>
    public async Task<Category?> GetByIdForUserAsync(int id, Guid userId)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && (c.IsSystemCategory || c.UserId == userId));
    }

    /// <summary>
    /// Checks whether a category with the given name already exists for the user.
    /// Comparison is case-insensitive via SQL Server's default collation — no
    /// <c>ToLower()</c> is applied so the Name index remains usable.
    /// Pass <paramref name="excludeId"/> to exclude the current record during updates.
    /// </summary>
    /// <param name="userId">The ID of the user whose categories are checked.</param>
    /// <param name="name">The category name to check for uniqueness.</param>
    /// <param name="excludeId">Optional category ID to exclude from the check (for update operations).</param>
    public async Task<bool> CategoryNameExistsAsync(Guid userId, string name, int? excludeId = null)
    {
        // SQL Server collation handles case-insensitive comparison at the database level.
        // Calling ToLower() here would translate to LOWER(Name) in SQL and prevent
        // the query engine from using the index on the Name column.
        var query = _dbSet.Where(c => c.UserId == userId && c.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}