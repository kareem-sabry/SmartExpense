using System.Globalization;
using SmartExpense.Application.Dtos.Budget;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Exceptions;
using SmartExpense.Core.Models;

namespace SmartExpense.Infrastructure.Services;

/// <summary>
///     Manages monthly budgets per category, including creation, tracking of actual spend,
///     and status calculation (Under, Approaching, Exceeded).
/// </summary>
public class BudgetService : IBudgetService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initialises a new instance of <see cref="BudgetService" />.</summary>
    /// <param name="unitOfWork">Unit of Work providing access to all repositories.</param>
    /// <param name="dateTimeProvider">Abstraction over the system clock for testability.</param>
    public BudgetService(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    ///     Returns all budgets for the given user, optionally filtered by month and/or year.
    ///     Each budget includes its current spend and percentage used, calculated live from transactions.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="month">Optional month filter (1–12).</param>
    /// <param name="year">Optional year filter.</param>
    public async Task<List<BudgetReadDto>> GetAllAsync(Guid userId, int? month, int? year,
        CancellationToken cancellationToken = default)
    {
        var budgets = await _unitOfWork.Budgets.GetAllForUserAsync(userId, month, year,cancellationToken);
        var budgetDtos = new List<BudgetReadDto>();

        foreach (var budget in budgets)
        {
            var dto = await MapToReadDtoAsync(budget, userId, cancellationToken);
            budgetDtos.Add(dto);
        }

        return budgetDtos;
    }

    /// <summary>
    ///     Returns a single budget by ID, scoped to the authenticated user.
    /// </summary>
    /// <param name="id">The budget ID.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="NotFoundException">
    ///     Thrown when the budget does not exist or belongs to a different user.
    /// </exception>
    public async Task<BudgetReadDto> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var budget = await _unitOfWork.Budgets.GetByIdForUserAsync(id, userId, cancellationToken);

        if (budget == null)
            throw new NotFoundException("Budget", id);

        return await MapToReadDtoAsync(budget, userId,cancellationToken);
    }

    /// <summary>
    ///     Returns an aggregated summary of all budgets for the given month and year,
    ///     including total budgeted, total spent, remaining, and counts by status.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="month">The target month (1–12).</param>
    /// <param name="year">The target year.</param>
    public async Task<BudgetSummaryDto> GetSummaryAsync(Guid userId, int month, int year,
        CancellationToken cancellationToken = default)
    {
        var budgets = await _unitOfWork.Budgets.GetByMonthYearAsync(userId, month, year, cancellationToken);
        var budgetDtos = new List<BudgetReadDto>();

        decimal totalBudgeted = 0;
        decimal totalSpent = 0;
        var budgetsExceeded = 0;
        var budgetsApproaching = 0;

        foreach (var budget in budgets)
        {
            var dto = await MapToReadDtoAsync(budget, userId,cancellationToken);
            budgetDtos.Add(dto);

            totalBudgeted += dto.Amount;
            totalSpent += dto.Spent;

            if (dto.Status == BudgetStatus.Exceeded)
                budgetsExceeded++;
            else if (dto.Status == BudgetStatus.Approaching)
                budgetsApproaching++;
        }

        var percentageUsed = totalBudgeted > 0 ? totalSpent / totalBudgeted * 100 : 0;

        return new BudgetSummaryDto
        {
            Month = month,
            Year = year,
            Period = GetPeriodDisplay(month, year),
            TotalBudgeted = totalBudgeted,
            TotalSpent = totalSpent,
            TotalRemaining = totalBudgeted - totalSpent,
            PercentageUsed = Math.Round(percentageUsed, 2),
            TotalBudgets = budgets.Count,
            BudgetsExceeded = budgetsExceeded,
            BudgetsApproaching = budgetsApproaching,
            Budgets = budgetDtos
        };
    }

    /// <summary>
    ///     Creates a new budget for a specific category and month/year period.
    ///     Prevents duplicate budgets for the same category and period.
    ///     Budgets cannot be created for past months.
    /// </summary>
    /// <param name="dto">The budget creation payload.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="NotFoundException">Thrown when the category does not exist or belongs to a different user.</exception>
    /// <exception cref="ConflictException">Thrown when a budget already exists for this category and period.</exception>
    /// <exception cref="ValidationException">Thrown when the target month is in the past.</exception>
    public async Task<BudgetReadDto> CreateAsync(BudgetCreateDto dto, Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Validate category exists
        var category = await _unitOfWork.Categories.GetByIdForUserAsync(dto.CategoryId, userId, cancellationToken);
        if (category == null)
            throw new NotFoundException("Category", dto.CategoryId);

        // Check if budget already exists for this category and period
        var exists = await _unitOfWork.Budgets.BudgetExistsAsync(
            userId,
            dto.CategoryId,
            dto.Month,
            dto.Year, cancellationToken: cancellationToken
        );

        if (exists)
            throw new ConflictException(
                $"Budget already exists for {category.Name} in {GetPeriodDisplay(dto.Month, dto.Year)}");

        // Validate period is not in the past (optional - you can allow past budgets)
        var budgetDate = new DateTime(dto.Year, dto.Month, 1);
        var currentDate = new DateTime(_dateTimeProvider.UtcNow.Year, _dateTimeProvider.UtcNow.Month, 1);

        if (budgetDate < currentDate)
            throw new ValidationException("Cannot create budget for past months");

        var budget = new Budget
        {
            UserId = userId,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Month = dto.Month,
            Year = dto.Year
        };

        await _unitOfWork.Budgets.AddAsync(budget, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _unitOfWork.Budgets.GetByIdForUserAsync(budget.Id, userId, cancellationToken);
        return await MapToReadDtoAsync(created!, userId, cancellationToken);
    }

    /// <summary>
    ///     Updates the budget amount for an existing budget. Only the amount can be changed;
    ///     the category and period are immutable after creation.
    /// </summary>
    /// <param name="id">The budget ID to update.</param>
    /// <param name="dto">The update payload containing the new amount.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="NotFoundException">Thrown when the budget does not exist or belongs to a different user.</exception>
    public async Task<BudgetReadDto> UpdateAsync(int id, BudgetUpdateDto dto, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var budget = await _unitOfWork.Budgets.GetByIdForUserAsync(id, userId, cancellationToken);

        if (budget == null)
            throw new NotFoundException("Budget", id);

        budget.Amount = dto.Amount;

        await _unitOfWork.Budgets.UpdateAsync(budget, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _unitOfWork.Budgets.GetByIdForUserAsync(id, userId, cancellationToken);
        return await MapToReadDtoAsync(updated!, userId, cancellationToken);
    }

    /// <summary>
    ///     Permanently deletes a budget. Previously recorded transactions are not affected.
    /// </summary>
    /// <param name="id">The budget ID to delete.</param>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <exception cref="NotFoundException">Thrown when the budget does not exist or belongs to a different user.</exception>
    public async Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var budget = await _unitOfWork.Budgets.GetByIdForUserAsync(id, userId, cancellationToken);

        if (budget == null)
            throw new NotFoundException("Budget", id);

        await _unitOfWork.Budgets.DeleteAsync(id, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    #region Private Helper Methods

    /// <summary>
    ///     Maps a <see cref="Budget" /> entity to a <see cref="BudgetReadDto" /> by calculating
    ///     the actual spend for the budget's category during its period via a single targeted query.
    /// </summary>
    /// <param name="budget">The budget entity to map.</param>
    /// <param name="userId">The ID of the user, used to scope the transaction query.</param>
    private async Task<BudgetReadDto> MapToReadDtoAsync(Budget budget, Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Calculate spent amount for this category in this month/year
        var startDate = new DateTime(budget.Year, budget.Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Filter by category
        var categoryTransactions = await _unitOfWork.Transactions.GetPagedAsync(
            userId,
            new TransactionQueryParameters
            {
                CategoryId = budget.CategoryId,
                StartDate = startDate,
                EndDate = endDate,
                TransactionType = TransactionType.Expense,
                PageSize = int.MaxValue
            }, cancellationToken
        );

        var actualSpent = categoryTransactions.Data.Sum(t => t.Amount);
        var remaining = budget.Amount - actualSpent;
        var percentageUsed = budget.Amount > 0 ? actualSpent / budget.Amount * 100 : 0;

        // Determine status
        var status = GetBudgetStatus(percentageUsed);

        return new BudgetReadDto
        {
            Id = budget.Id,
            CategoryId = budget.CategoryId,
            CategoryName = budget.Category?.Name ?? string.Empty,
            CategoryIcon = budget.Category?.Icon,
            CategoryColor = budget.Category?.Color,
            Amount = budget.Amount,
            Month = budget.Month,
            Year = budget.Year,
            Period = GetPeriodDisplay(budget.Month, budget.Year),
            Spent = actualSpent,
            Remaining = remaining,
            PercentageUsed = Math.Round(percentageUsed, 2),
            Status = status,
            StatusDisplay = status.ToString(),
            CreatedAtUtc = budget.CreatedAtUtc,
            UpdatedAtUtc = budget.UpdatedAtUtc
        };
    }

    /// <summary>
    ///     Determines the <see cref="BudgetStatus" /> based on how much of the budget has been used.
    ///     Under 80% → <see cref="BudgetStatus.UnderBudget" />,
    ///     80–99% → <see cref="BudgetStatus.Approaching" />,
    ///     100%+ → <see cref="BudgetStatus.Exceeded" />.
    /// </summary>
    private static BudgetStatus GetBudgetStatus(decimal percentageUsed)
    {
        if (percentageUsed >= 100)
            return BudgetStatus.Exceeded;
        if (percentageUsed >= 80)
            return BudgetStatus.Approaching;
        return BudgetStatus.UnderBudget;
    }

    private static string GetPeriodDisplay(int month, int year)
    {
        var date = new DateTime(year, month, 1);
        return date.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }

    #endregion
}