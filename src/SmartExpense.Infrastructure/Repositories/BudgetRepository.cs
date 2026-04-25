using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Infrastructure.Data;

namespace SmartExpense.Infrastructure.Repositories;

/// <summary>
///     Provides data access operations for <see cref="Budget" /> entities.
///     Read-only queries use <c>AsNoTracking()</c> for reduced memory overhead.
/// </summary>
public class BudgetRepository : GenericRepository<Budget>, IBudgetRepository
{
    /// <summary>Initialises a new instance of <see cref="BudgetRepository" />.</summary>
    public BudgetRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    ///     Returns a single budget by ID scoped to the authenticated user.
    ///     Returns <c>null</c> if the budget does not exist or belongs to a different user.
    /// </summary>
    public async Task<Budget?> GetByIdForUserAsync(int id, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(b => b.Category)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId, cancellationToken);
    }

    /// <summary>
    ///     Returns all budgets for the given user, optionally filtered by month and/or year.
    ///     Ordered by year descending, then month descending, then category name ascending.
    /// </summary>
    public async Task<List<Budget>> GetAllForUserAsync(Guid userId, int? month, int? year,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Budget> query = _dbSet
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .Include(b => b.Category);

        if (month.HasValue) query = query.Where(b => b.Month == month.Value);

        if (year.HasValue) query = query.Where(b => b.Year == year.Value);

        return await query
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.Month)
            .ThenBy(b => b.Category.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Returns the budget for a specific category and period, or <c>null</c> if none exists.
    /// </summary>
    public async Task<Budget?> GetByCategoryAndPeriodAsync(Guid userId, int categoryId, int month, int year,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(b => b.Category)
            .FirstOrDefaultAsync(b =>
                b.UserId == userId &&
                b.CategoryId == categoryId &&
                b.Month == month &&
                b.Year == year, cancellationToken);
    }

    /// <summary>
    ///     Returns <c>true</c> if a budget already exists for the given user, category, and period.
    ///     Pass <paramref name="excludeBudgetId" /> to exclude a specific record (useful during update).
    /// </summary>
    public async Task<bool> BudgetExistsAsync(
        Guid userId,
        int categoryId,
        int month,
        int year,
        int? excludeBudgetId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(b =>
            b.UserId == userId &&
            b.CategoryId == categoryId &&
            b.Month == month &&
            b.Year == year);

        if (excludeBudgetId.HasValue) query = query.Where(b => b.Id != excludeBudgetId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>
    ///     Returns all budgets for the given user, month, and year.
    ///     Used by analytics and budget performance calculations.
    /// </summary>
    public async Task<List<Budget>> GetByMonthYearAsync(Guid userId, int month, int year,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(b => b.UserId == userId && b.Month == month && b.Year == year)
            .Include(b => b.Category)
            .ToListAsync(cancellationToken);
    }
}