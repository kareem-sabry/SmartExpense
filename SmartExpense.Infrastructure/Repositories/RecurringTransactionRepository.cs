using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Infrastructure.Data;

namespace SmartExpense.Infrastructure.Repositories;

/// <summary>
/// Provides data access operations for <see cref="RecurringTransaction"/> entities.
/// Read-only queries use <c>AsNoTracking()</c> for reduced memory overhead.
/// </summary>
public class RecurringTransactionRepository : GenericRepository<RecurringTransaction>, IRecurringTransactionRepository
{
    /// <summary>Initialises a new instance of <see cref="RecurringTransactionRepository"/>.</summary>
    public RecurringTransactionRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Returns a single recurring transaction template by ID, scoped to the authenticated user.
    /// Returns <c>null</c> if the template does not exist or belongs to a different user.
    /// </summary>
    public async Task<RecurringTransaction?> GetByIdForUserAsync(int id, Guid userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
    }

    /// <summary>
    /// Returns all recurring transaction templates for the given user.
    /// Active templates appear first, then ordered alphabetically by description.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="isActive">
    /// Optional filter: <c>true</c> returns only active templates,
    /// <c>false</c> returns only inactive, <c>null</c> returns all.
    /// </param>
    public async Task<List<RecurringTransaction>> GetAllForUserAsync(Guid userId, bool? isActive = null)
    {
        IQueryable<RecurringTransaction> query = _dbSet
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Include(r => r.Category);

        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        return await query
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.Description)
            .ToListAsync();
    }

    /// <summary>
    /// Returns all active recurring templates whose start date has passed and whose
    /// end date (if set) has not yet been reached as of <paramref name="asOfDate"/>.
    /// Used by the background worker to determine which templates need processing.
    /// </summary>
    /// <param name="asOfDate">The reference date to check against start and end dates.</param>
    public async Task<List<RecurringTransaction>> GetDueForGenerationAsync(DateTime asOfDate)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(r => r.Category)
            .Where(r =>
                r.IsActive &&
                r.StartDate <= asOfDate &&
                (r.EndDate == null || r.EndDate >= asOfDate)
            )
            .ToListAsync();
    }
}