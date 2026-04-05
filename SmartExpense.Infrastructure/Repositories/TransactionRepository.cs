using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Models;
using SmartExpense.Infrastructure.Data;

namespace SmartExpense.Infrastructure.Repositories;

/// <summary>
///     Provides data access operations for <see cref="Transaction" /> entities.
///     All read-only queries use <c>AsNoTracking()</c> for improved memory and CPU performance,
///     since returned entities are mapped to DTOs and never modified within the same operation.
///     Write paths (Add, Update, Delete) use tracked entities as required by EF Core.
/// </summary>
public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
{
    /// <summary>Initialises a new instance of <see cref="TransactionRepository" />.</summary>
    public TransactionRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    ///     Returns a paginated, filtered, and sorted list of transactions for the given user.
    ///     Supports filtering by category, transaction type, date range, amount range, and free-text search.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="parameters">Filtering, sorting, and pagination parameters.</param>
    public async Task<PagedResult<Transaction>> GetPagedAsync(
        Guid userId,
        TransactionQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Transaction> query = _dbSet
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Include(t => t.Category);

        // Apply filters
        query = ApplyFilters(query, parameters);

        // Apply sorting
        query = ApplySorting(query, parameters);

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);
        // Apply pagination
        var data = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Transaction>
        {
            Data = data,
            PageNumber = parameters.PageNumber,
            PageSize = parameters.PageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    ///     Returns the most recent transactions for the given user, ordered by transaction date descending.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="count">Maximum number of results to return. Defaults to 10.</param>
    public async Task<List<Transaction>> GetRecentAsync(Guid userId, int count = 10,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Include(t => t.Category)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Returns the total income amount for the given user within the optional date range.
    ///     Returns zero when no matching transactions exist.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">Optional lower bound on <c>TransactionDate</c> (inclusive).</param>
    /// <param name="endDate">Optional upper bound on <c>TransactionDate</c> (inclusive).</param>
    public async Task<decimal> GetTotalIncomeAsync(Guid userId, DateTime? startDate, DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(t =>
            t.UserId == userId &&
            t.TransactionType == TransactionType.Income);

        query = ApplyDateFilter(query, startDate, endDate);

        return await query.SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;
    }

    /// <summary>
    ///     Returns the total expense amount for the given user within the optional date range.
    ///     Returns zero when no matching transactions exist.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">Optional lower bound on <c>TransactionDate</c> (inclusive).</param>
    /// <param name="endDate">Optional upper bound on <c>TransactionDate</c> (inclusive).</param>
    public async Task<decimal> GetTotalExpenseAsync(Guid userId, DateTime? startDate, DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(t =>
            t.UserId == userId &&
            t.TransactionType == TransactionType.Expense);

        query = ApplyDateFilter(query, startDate, endDate);

        return await query.SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;
    }

    /// <summary>
    ///     Returns the total number of transactions for the given user within the optional date range.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">Optional lower bound on <c>TransactionDate</c> (inclusive).</param>
    /// <param name="endDate">Optional upper bound on <c>TransactionDate</c> (inclusive).</param>
    public async Task<int> GetTransactionCountAsync(Guid userId, DateTime? startDate, DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(t => t.UserId == userId);
        query = ApplyDateFilter(query, startDate, endDate);
        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    ///     Returns a single transaction by ID, scoped to the authenticated user.
    ///     Returns <c>null</c> if the transaction does not exist or belongs to a different user.
    /// </summary>
    /// <param name="id">The transaction ID.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    public async Task<Transaction?> GetByIdForUserAsync(int id, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsForRecurringOnDateAsync(int recurringTransactionId, DateTime date,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(t =>
            t.RecurringTransactionId == recurringTransactionId &&
            t.TransactionDate.Date == date.Date, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetCountByTypeAsync(
        Guid userId,
        TransactionType type,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.TransactionType == type);

        query = ApplyDateFilter(query, startDate, endDate);

        return await query.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<CategorySpendSummary>> GetCategorySpendSummaryAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        TransactionType type,
        CancellationToken cancellationToken = default)
    {
        // Step 1 — SQL GROUP BY: SUM + COUNT per CategoryId.
        // Anonymous-type GroupBy is guaranteed to translate to SQL in EF Core.
        var grouped = await _dbSet
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId &&
                t.TransactionType == type &&
                t.TransactionDate >= startDate &&
                t.TransactionDate <= endDate)
            .GroupBy(t => t.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                TotalAmount = g.Sum(t => t.Amount),
                TransactionCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        if (grouped.Count == 0)
            return [];

        // Step 2 — One IN query for category metadata (name / icon / color).
        var categoryIds = grouped.Select(g => g.CategoryId).ToList();
        var categories = await _context.Set<Category>()
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Icon, c.Color })
            .ToListAsync(cancellationToken);
        var categoryMap = categories.ToDictionary(c => c.Id);

        // Step 3 — In-memory join of two small result sets.
        return grouped
            .Select(g =>
            {
                categoryMap.TryGetValue(g.CategoryId, out var cat);
                return new CategorySpendSummary(
                    g.CategoryId,
                    cat?.Name ?? "Unknown",
                    cat?.Icon,
                    cat?.Color,
                    g.TotalAmount,
                    g.TransactionCount);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<TransactionTrendProjection>> GetTrendProjectionsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // SELECT TransactionDate, TransactionType, Amount WHERE ...
        // EF Core projects these three columns; no entity graph is loaded.
        return await _dbSet
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId &&
                t.TransactionDate >= startDate &&
                t.TransactionDate <= endDate)
            .Select(t => new TransactionTrendProjection(
                t.TransactionDate,
                t.TransactionType,
                t.Amount))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal> GetActualSpentAsync(
        Guid userId,
        int categoryId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId &&
                t.CategoryId == categoryId &&
                t.TransactionType == TransactionType.Expense &&
                t.TransactionDate >= startDate &&
                t.TransactionDate <= endDate)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0;
    }

    #region Private Helper Methods

    /// <summary>
    ///     Applies all active filters from <paramref name="parameters" /> to the query.
    ///     Text search does not use <c>ToLower()</c> — SQL Server collation handles
    ///     case-insensitive comparison at the database level without sacrificing index use.
    /// </summary>
    private static IQueryable<Transaction> ApplyFilters(
        IQueryable<Transaction> query,
        TransactionQueryParameters parameters)
    {
        // Search term filter
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var searchTerm = parameters.SearchTerm;
            query = query.Where(t =>
                t.Description.Contains(searchTerm) ||
                (t.Notes != null && t.Notes.Contains(searchTerm)));
        }

        // Category filter
        if (parameters.CategoryId.HasValue) query = query.Where(t => t.CategoryId == parameters.CategoryId.Value);

        // Transaction type filter
        if (parameters.TransactionType.HasValue)
            query = query.Where(t => t.TransactionType == parameters.TransactionType.Value);

        // Date range filter
        query = ApplyDateFilter(query, parameters.StartDate, parameters.EndDate);

        // Amount range filter
        if (parameters.MinAmount.HasValue) query = query.Where(t => t.Amount >= parameters.MinAmount.Value);

        if (parameters.MaxAmount.HasValue) query = query.Where(t => t.Amount <= parameters.MaxAmount.Value);

        return query;
    }

    /// <summary>
    ///     Applies an optional inclusive date range filter on <c>TransactionDate</c>.
    /// </summary>
    private static IQueryable<Transaction> ApplyDateFilter(
        IQueryable<Transaction> query,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue) query = query.Where(t => t.TransactionDate >= startDate.Value);

        if (endDate.HasValue) query = query.Where(t => t.TransactionDate <= endDate.Value);

        return query;
    }

    /// <summary>
    ///     Applies sorting to the query based on <see cref="TransactionQueryParameters.SortBy" />
    ///     and <see cref="TransactionQueryParameters.SortDescending" />.
    ///     Defaults to <c>TransactionDate DESC, CreatedAtUtc DESC</c> for unrecognised sort fields.
    /// </summary>
    private static IQueryable<Transaction> ApplySorting(
        IQueryable<Transaction> query,
        TransactionQueryParameters parameters)
    {
        return parameters.SortBy.ToLower() switch
        {
            "amount" => parameters.SortDescending
                ? query.OrderByDescending(t => t.Amount)
                : query.OrderBy(t => t.Amount),
            "description" => parameters.SortDescending
                ? query.OrderByDescending(t => t.Description)
                : query.OrderBy(t => t.Description),
            "category" => parameters.SortDescending
                ? query.OrderByDescending(t => t.Category.Name)
                : query.OrderBy(t => t.Category.Name),
            _ => parameters.SortDescending
                ? query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.CreatedAtUtc)
                : query.OrderBy(t => t.TransactionDate).ThenBy(t => t.CreatedAtUtc)
        };
    }

    #endregion
}