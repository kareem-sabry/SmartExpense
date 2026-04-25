using System.Text;
using SmartExpense.Application.Dtos.Transaction;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Exceptions;
using SmartExpense.Core.Models;

namespace SmartExpense.Infrastructure.Services;

public class TransactionService : ITransactionService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public TransactionService(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    ///     Returns a paginated, optionally filtered list of transactions for the given user.
    ///     Filtering and sorting are delegated to the repository layer so only the requested
    ///     page is loaded into memory.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user whose transactions to query.</param>
    /// <param name="parameters">Pagination options (page, pageSize) and filters (date range, type, categoryId).</param>
    /// <returns>A <see cref="PagedResult{T}" /> of mapped <see cref="TransactionReadDto" /> objects.</returns>
    public async Task<PagedResult<TransactionReadDto>> GetPagedAsync(
        Guid userId,
        TransactionQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var pagedResult = await _unitOfWork.Transactions.GetPagedAsync(userId, parameters, cancellationToken);

        return new PagedResult<TransactionReadDto>
        {
            Data = pagedResult.Data.Select(MapToReadDto).ToList(),
            PageNumber = pagedResult.PageNumber,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount
        };
    }

    /// <summary>
    ///     Returns a single transaction by its primary key, scoped to the given user.
    ///     Ownership is enforced at the repository level, so a transaction belonging to
    ///     another user is treated identically to a non-existent one.
    /// </summary>
    /// <param name="id">The primary key of the transaction.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <returns>The mapped <see cref="TransactionReadDto" />.</returns>
    /// <exception cref="NotFoundException">Thrown when no transaction with <paramref name="id" /> exists for this user.</exception>
    public async Task<TransactionReadDto> GetByIdAsync(int id, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdForUserAsync(id, userId, cancellationToken);

        if (transaction == null)
            throw new NotFoundException("Transaction", id);

        return MapToReadDto(transaction);
    }

    /// <summary>
    ///     Returns the <paramref name="count" /> most recent transactions for the given user,
    ///     ordered by <c>TransactionDate</c> descending. Intended for dashboard widgets
    ///     where only a small, unfiltered slice of history is needed.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="count">Maximum number of transactions to return. Defaults to 10.</param>
    /// <returns>A list of the most recent <see cref="TransactionReadDto" /> objects.</returns>
    public async Task<List<TransactionReadDto>> GetRecentAsync(Guid userId, int count = 10,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _unitOfWork.Transactions.GetRecentAsync(userId, count, cancellationToken);
        return transactions.Select(MapToReadDto).ToList();
    }

    /// <summary>
    ///     Calculates an aggregated financial summary for the given user within an optional
    ///     date range. Three separate repository calls are made so each aggregate can be
    ///     computed with a targeted, index-friendly query.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">Optional inclusive start of the date range. Omit to include all history.</param>
    /// <param name="endDate">Optional inclusive end of the date range. Omit to include up to the present.</param>
    /// <returns>
    ///     A <see cref="TransactionSummaryDto" /> containing total income, total expenses,
    ///     net balance, and transaction count for the period.
    /// </returns>
    public async Task<TransactionSummaryDto> GetSummaryAsync(
        Guid userId,
        DateTime? startDate,
        DateTime? endDate, CancellationToken cancellationToken = default)
    {
        var totalIncome =
            await _unitOfWork.Transactions.GetTotalIncomeAsync(userId, startDate, endDate, cancellationToken);
        var totalExpense =
            await _unitOfWork.Transactions.GetTotalExpenseAsync(userId, startDate, endDate, cancellationToken);
        var transactionCount =
            await _unitOfWork.Transactions.GetTransactionCountAsync(userId, startDate, endDate, cancellationToken);

        return new TransactionSummaryDto
        {
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            NetBalance = totalIncome - totalExpense,
            TransactionCount = transactionCount,
            StartDate = startDate,
            EndDate = endDate
        };
    }

    /// <summary>
    ///     Exports all transactions for the given user within the specified date range as a
    ///     UTF-8 CSV string. Columns: Date, Description, Category, Type, Amount, Notes.
    ///     Fields that may contain commas or quotes are double-quoted.
    ///     The entire result set is loaded in a single page to avoid partial exports.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">Inclusive start date of the export window.</param>
    /// <param name="endDate">Inclusive end date of the export window.</param>
    /// <returns>A CSV-formatted string ready to be encoded and streamed to the client.</returns>
    public async Task<string> ExportToCsvAsync(Guid userId, DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 1000;
        var pageNumber = 1;

        var sb = new StringBuilder();
        sb.AppendLine("Date,Description,Category,Type,Amount,Notes");

        while (true)
        {
            var result = await _unitOfWork.Transactions.GetPagedAsync(userId,
                new TransactionQueryParameters
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                }, cancellationToken);

            foreach (var t in result.Data)
                sb.AppendLine(
                    $"{t.TransactionDate:yyyy-MM-dd}," +
                    $"\"{EscapeCsv(t.Description)}\"," +
                    $"\"{EscapeCsv(t.Category?.Name)}\"," +
                    $"{t.TransactionType}," +
                    $"{t.Amount}," +
                    $"\"{EscapeCsv(t.Notes)}\"");

            if (result.Data.Count < pageSize)  // ← last page reached
                break;

            pageNumber++;
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Creates a new transaction for the given user after validating that the target
    ///     category exists, belongs to the user, is active, and that the transaction date
    ///     is not in the future. Audit fields are populated by the <c>AuditInterceptor</c>
    ///     and not set here.
    /// </summary>
    /// <param name="dto">The data for the new transaction.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <returns>The persisted transaction mapped to a <see cref="TransactionReadDto" />, including the generated ID.</returns>
    /// <exception cref="NotFoundException">Thrown when the specified category does not exist for this user.</exception>
    /// <exception cref="ValidationException">Thrown when the category is inactive or the date is in the future.</exception>
    public async Task<TransactionReadDto> CreateAsync(TransactionCreateDto dto, Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Validate category exists and user has access to it
        var category = await _unitOfWork.Categories.GetByIdForUserAsync(dto.CategoryId, userId, cancellationToken);
        if (category == null)
            throw new NotFoundException("Category", dto.CategoryId);

        if (!category.IsActive)
            throw new ValidationException("Cannot create transaction with inactive category");

        // Validate transaction date is not in future
        if (dto.TransactionDate > _dateTimeProvider.UtcNow)
            throw new ValidationException("Transaction date cannot be in the future");

        var transaction = new Transaction
        {
            UserId = userId,
            CategoryId = dto.CategoryId,
            Description = dto.Description,
            Amount = dto.Amount,
            TransactionType = dto.TransactionType,
            TransactionDate = dto.TransactionDate,
            Notes = dto.Notes
            // CreatedAtUtc and CreatedBy will be set by AuditInterceptor
        };

        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload to get category navigation property
        var created = await _unitOfWork.Transactions.GetByIdForUserAsync(transaction.Id, userId, cancellationToken);
        return MapToReadDto(created!);
    }

    /// <summary>
    ///     Updates the mutable fields of an existing transaction that belongs to the given user.
    ///     Applies the same category and date validations as <see cref="CreateAsync" />.
    ///     Audit fields are updated by the <c>AuditInterceptor</c> and not set here.
    /// </summary>
    /// <param name="id">The ID of the transaction to update.</param>
    /// <param name="dto">The updated field values.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <returns>The updated transaction mapped to a <see cref="TransactionReadDto" />.</returns>
    /// <exception cref="NotFoundException">Thrown when the transaction or category does not exist for this user.</exception>
    /// <exception cref="ValidationException">Thrown when the category is inactive or the date is in the future.</exception>
    public async Task<TransactionReadDto> UpdateAsync(int id, TransactionUpdateDto dto, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdForUserAsync(id, userId, cancellationToken);

        if (transaction == null)
            throw new NotFoundException("Transaction", id);

        // Validate category exists and user has access to it
        var category = await _unitOfWork.Categories.GetByIdForUserAsync(dto.CategoryId, userId, cancellationToken);
        if (category == null)
            throw new NotFoundException("Category", dto.CategoryId);

        if (!category.IsActive)
            throw new ValidationException("Cannot update transaction with inactive category");

        // Validate transaction date is not in future
        if (dto.TransactionDate > _dateTimeProvider.UtcNow)
            throw new ValidationException("Transaction date cannot be in the future");

        // Update transaction
        transaction.CategoryId = dto.CategoryId;
        transaction.Description = dto.Description;
        transaction.Amount = dto.Amount;
        transaction.TransactionType = dto.TransactionType;
        transaction.TransactionDate = dto.TransactionDate;
        transaction.Notes = dto.Notes;
        // UpdatedAtUtc and UpdatedBy will be set by AuditInterceptor

        await _unitOfWork.Transactions.UpdateAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload to get updated category navigation property
        var updated = await _unitOfWork.Transactions.GetByIdForUserAsync(id, userId, cancellationToken);
        return MapToReadDto(updated!);
    }

    /// <summary>
    ///     Permanently deletes a transaction that belongs to the given user.
    ///     Ownership is verified before deletion; attempting to delete another user's
    ///     transaction throws <see cref="NotFoundException" /> rather than revealing its existence.
    /// </summary>
    /// <param name="id">The ID of the transaction to delete.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="NotFoundException">Thrown when no transaction with <paramref name="id" /> exists for this user.</exception>
    public async Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdForUserAsync(id, userId, cancellationToken);

        if (transaction == null)
            throw new NotFoundException("Transaction", id);

        await _unitOfWork.Transactions.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\"", "\"\"");
    }

    #region Private Helper Methods

    /// <summary>
    ///     Maps a <see cref="Transaction" /> entity to its read-only DTO representation.
    ///     Category navigation properties are accessed safely; a missing nav property
    ///     results in empty strings rather than a <see cref="NullReferenceException" />.
    /// </summary>
    /// <param name="transaction">The entity to map. Must not be <c>null</c>.</param>
    /// <returns>A fully populated <see cref="TransactionReadDto" />.</returns>
    private static TransactionReadDto MapToReadDto(Transaction transaction)
    {
        return new TransactionReadDto
        {
            Id = transaction.Id,
            CategoryId = transaction.CategoryId,
            CategoryName = transaction.Category?.Name ?? string.Empty,
            CategoryIcon = transaction.Category?.Icon,
            CategoryColor = transaction.Category?.Color,
            Description = transaction.Description,
            Amount = transaction.Amount,
            TransactionType = transaction.TransactionType,
            TransactionTypeDisplay = transaction.TransactionType.ToString(),
            TransactionDate = transaction.TransactionDate,
            Notes = transaction.Notes,
            CreatedAtUtc = transaction.CreatedAtUtc,
            UpdatedAtUtc = transaction.UpdatedAtUtc
        };
    }

    #endregion
}