using SmartExpense.Application.Dtos.RecurringTransaction;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Exceptions;

namespace SmartExpense.Infrastructure.Services;

public class RecurringTransactionService : IRecurringTransactionService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public RecurringTransactionService(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<List<RecurringTransactionReadDto>> GetAllAsync(Guid userId, bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var recurringTransactions =
            await _unitOfWork.RecurringTransactions.GetAllForUserAsync(userId, isActive, cancellationToken);
        return recurringTransactions.Select(MapToReadDto).ToList();
    }

    public async Task<RecurringTransactionReadDto> GetByIdAsync(int id, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var recurring = await _unitOfWork.RecurringTransactions.GetByIdForUserAsync(id, userId, cancellationToken);

        if (recurring == null)
            throw new NotFoundException("RecurringTransaction", id);

        return MapToReadDto(recurring);
    }

    public async Task<RecurringTransactionReadDto> CreateAsync(RecurringTransactionCreateDto dto, Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Validate category exists
        var category = await _unitOfWork.Categories.GetByIdForUserAsync(dto.CategoryId, userId, cancellationToken);
        if (category == null)
            throw new NotFoundException("Category", dto.CategoryId);

        if (!category.IsActive)
            throw new ValidationException("Cannot create recurring transaction with inactive category");

        // Validate dates
        if (dto.EndDate.HasValue && dto.EndDate.Value < dto.StartDate)
            throw new ValidationException("End date cannot be before start date");

        var recurring = new RecurringTransaction
        {
            UserId = userId,
            CategoryId = dto.CategoryId,
            Description = dto.Description,
            Amount = dto.Amount,
            TransactionType = dto.TransactionType,
            Frequency = dto.Frequency,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Notes = dto.Notes,
            IsActive = true
        };

        await _unitOfWork.RecurringTransactions.AddAsync(recurring, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created =
            await _unitOfWork.RecurringTransactions.GetByIdForUserAsync(recurring.Id, userId, cancellationToken);
        return MapToReadDto(created!);
    }

    public async Task<RecurringTransactionReadDto> UpdateAsync(int id, RecurringTransactionUpdateDto dto, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var recurring = await _unitOfWork.RecurringTransactions.GetByIdForUserAsync(id, userId, cancellationToken);

        if (recurring == null)
            throw new NotFoundException("RecurringTransaction", id);

        // Validate category
        var category = await _unitOfWork.Categories.GetByIdForUserAsync(dto.CategoryId, userId, cancellationToken);
        if (category == null)
            throw new NotFoundException("Category", dto.CategoryId);

        if (!category.IsActive)
            throw new ValidationException("Cannot update recurring transaction with inactive category");

        // Validate end date
        if (dto.EndDate.HasValue && dto.EndDate.Value < recurring.StartDate)
            throw new ValidationException("End date cannot be before start date");

        // Update fields
        recurring.CategoryId = dto.CategoryId;
        recurring.Description = dto.Description;
        recurring.Amount = dto.Amount;
        recurring.TransactionType = dto.TransactionType;
        recurring.Frequency = dto.Frequency;
        recurring.EndDate = dto.EndDate;
        recurring.Notes = dto.Notes;

        await _unitOfWork.RecurringTransactions.UpdateAsync(recurring, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.RecurringTransactions.GetByIdForUserAsync(id, userId, cancellationToken);
        return MapToReadDto(updated!);
    }

    public async Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var recurring = await _unitOfWork.RecurringTransactions.GetByIdForUserAsync(id, userId, cancellationToken);

        if (recurring == null)
            throw new NotFoundException("RecurringTransaction", id);

        await _unitOfWork.RecurringTransactions.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<RecurringTransactionReadDto> ToggleActiveAsync(int id, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var recurring = await _unitOfWork.RecurringTransactions.GetByIdForUserAsync(id, userId, cancellationToken);

        if (recurring == null)
            throw new NotFoundException("RecurringTransaction", id);

        recurring.IsActive = !recurring.IsActive;

        await _unitOfWork.RecurringTransactions.UpdateAsync(recurring, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToReadDto(recurring);
    }

    public async Task<GenerateTransactionsResultDto> GenerateTransactionsAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var dueRecurring = await _unitOfWork.RecurringTransactions.GetAllForUserAsync(userId, true, cancellationToken);

        var result = new GenerateTransactionsResultDto();

        foreach (var recurring in dueRecurring)
        {
            var generated = await GenerateTransactionsForRecurring(recurring, now, cancellationToken);
            result.GeneratedTransactions.AddRange(generated);
        }

        result.TransactionsGenerated = result.GeneratedTransactions.Count;
        return result;
    }

    public async Task<GenerateTransactionsResultDto> GenerateForRecurringTransactionAsync(int recurringId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var recurring =
            await _unitOfWork.RecurringTransactions.GetByIdForUserAsync(recurringId, userId, cancellationToken);

        if (recurring == null)
            throw new NotFoundException("RecurringTransaction", recurringId);

        var now = _dateTimeProvider.UtcNow;
        var generated = await GenerateTransactionsForRecurring(recurring, now, cancellationToken);

        return new GenerateTransactionsResultDto
        {
            TransactionsGenerated = generated.Count,
            GeneratedTransactions = generated
        };
    }

    #region Private Helper Methods

    private async Task<List<GeneratedTransactionInfo>> GenerateTransactionsForRecurring(
        RecurringTransaction recurring,
        DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var generatedInfo = new List<GeneratedTransactionInfo>();

        // Calculate next due dates
        var dueDates = CalculateDueDates(recurring, asOfDate);

        foreach (var dueDate in dueDates)
        {
            // FK-based dedup: reliable regardless of description text changes
            var exists = await TransactionExistsForDate(recurring.Id, dueDate, cancellationToken);
            if (exists)
                continue;

            var transaction = new Transaction
            {
                UserId = recurring.UserId,
                CategoryId = recurring.CategoryId,
                Description = $"{recurring.Description} (Auto-generated)",
                Amount = recurring.Amount,
                TransactionType = recurring.TransactionType,
                TransactionDate = dueDate,
                Notes = recurring.Notes,
                // Stamp the FK so deduplication is reliable on future runs
                RecurringTransactionId = recurring.Id
            };

            await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);

            generatedInfo.Add(new GeneratedTransactionInfo
            {
                RecurringTransactionId = recurring.Id,
                Description = transaction.Description,
                Amount = transaction.Amount,
                TransactionDate = dueDate
            });
        }

        // Update last generated date
        recurring.LastGeneratedDate = asOfDate;
        await _unitOfWork.RecurringTransactions.UpdateAsync(recurring, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return generatedInfo;
    }

    private List<DateTime> CalculateDueDates(RecurringTransaction recurring, DateTime asOfDate)
    {
        var dueDates = new List<DateTime>();
        var lastGenerated = recurring.LastGeneratedDate ?? recurring.StartDate.AddDays(-1);
        var currentDate = GetNextOccurrence(recurring.StartDate, lastGenerated, recurring.Frequency);

        while (currentDate <= asOfDate)
        {
            // Check if within valid date range
            if (currentDate >= recurring.StartDate &&
                (!recurring.EndDate.HasValue || currentDate <= recurring.EndDate.Value))
                dueDates.Add(currentDate);

            currentDate = GetNextOccurrence(recurring.StartDate, currentDate, recurring.Frequency);

            // Safety check to prevent infinite loops
            if (dueDates.Count >= 100)
                break;
        }

        return dueDates;
    }

    private static DateTime GetNextOccurrence(DateTime startDate, DateTime fromDate, RecurrenceFrequency frequency)
    {
        return frequency switch
        {
            RecurrenceFrequency.Daily => fromDate.AddDays(1),
            RecurrenceFrequency.Weekly => fromDate.AddDays(7),
            RecurrenceFrequency.Monthly => fromDate.AddMonths(1),
            RecurrenceFrequency.Yearly => fromDate.AddYears(1),
            _ => fromDate.AddDays(1)
        };
    }

    /// <summary>
    ///     Checks whether a transaction has already been generated for a given recurring
    ///     template on a specific date using a targeted FK lookup.
    ///     This replaces the previous string-based approach which matched on description
    ///     text and would break if the description suffix changed.
    /// </summary>
    private async Task<bool> TransactionExistsForDate(int recurringId, DateTime date,
        CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Transactions.ExistsForRecurringOnDateAsync(recurringId, date, cancellationToken);
    }

    private RecurringTransactionReadDto MapToReadDto(RecurringTransaction recurring)
    {
        var nextDueDate = CalculateNextDueDate(recurring);

        return new RecurringTransactionReadDto
        {
            Id = recurring.Id,
            CategoryId = recurring.CategoryId,
            CategoryName = recurring.Category?.Name ?? string.Empty,
            CategoryIcon = recurring.Category?.Icon,
            CategoryColor = recurring.Category?.Color,
            Description = recurring.Description,
            Amount = recurring.Amount,
            TransactionType = recurring.TransactionType,
            TransactionTypeDisplay = recurring.TransactionType.ToString(),
            Frequency = recurring.Frequency,
            FrequencyDisplay = recurring.Frequency.ToString(),
            StartDate = recurring.StartDate,
            EndDate = recurring.EndDate,
            LastGeneratedDate = recurring.LastGeneratedDate,
            NextDueDate = nextDueDate,
            IsActive = recurring.IsActive,
            Notes = recurring.Notes,
            CreatedAtUtc = recurring.CreatedAtUtc,
            UpdatedAtUtc = recurring.UpdatedAtUtc
        };
    }

    private DateTime? CalculateNextDueDate(RecurringTransaction recurring)
    {
        if (!recurring.IsActive)
            return null;

        try
        {
            var lastGenerated = recurring.LastGeneratedDate ?? recurring.StartDate.AddDays(-1);
            var nextDate = GetNextOccurrence(recurring.StartDate, lastGenerated, recurring.Frequency);

            // Check if next date is beyond end date
            if (recurring.EndDate.HasValue && nextDate > recurring.EndDate.Value)
                return null;

            // Safety check for valid date range
            if (nextDate > DateTime.MaxValue.AddYears(-1) || nextDate < DateTime.MinValue.AddYears(1))
                return null;

            return nextDate;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    #endregion
}