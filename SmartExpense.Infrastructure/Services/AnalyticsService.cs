using System.Globalization;
using SmartExpense.Application.Dtos.Analytics;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Models;

namespace SmartExpense.Infrastructure.Services;

/// <summary>
///     Provides financial analytics including spending trends, category breakdowns,
///     month-over-month comparisons, and budget performance tracking.
///     All date/time operations use the injected <see cref="IDateTimeProvider" />
///     to ensure deterministic behaviour and full testability.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Initialises a new instance of <see cref="AnalyticsService" />.</summary>
    /// <param name="unitOfWork">Unit of Work providing access to all repositories.</param>
    /// <param name="dateTimeProvider">
    ///     Abstraction over the system clock. Injected so tests can control the current date
    ///     without relying on <see cref="DateTime.UtcNow" /> directly.
    /// </param>
    public AnalyticsService(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    ///     Returns a complete financial overview for the given date range, including totals,
    ///     savings rate, average daily income/expense, transaction counts, top categories,
    ///     and a daily spending trend.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">The inclusive start of the reporting period (UTC).</param>
    /// <param name="endDate">The inclusive end of the reporting period (UTC).</param>
    public async Task<FinancialOverviewDto> GetFinancialOverviewAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate, CancellationToken cancellationToken = default)
    {
        var totalIncome =
            await _unitOfWork.Transactions.GetTotalIncomeAsync(userId, startDate, endDate, cancellationToken);
        var totalExpense =
            await _unitOfWork.Transactions.GetTotalExpenseAsync(userId, startDate, endDate, cancellationToken);
        var transactionCount =
            await _unitOfWork.Transactions.GetTransactionCountAsync(userId, startDate, endDate, cancellationToken);

        var incomeCount = await _unitOfWork.Transactions.GetCountByTypeAsync(
            userId, TransactionType.Income, startDate, endDate, cancellationToken);
        var expenseCount = await _unitOfWork.Transactions.GetCountByTypeAsync(
            userId, TransactionType.Expense, startDate, endDate, cancellationToken);

        var days = (endDate - startDate).Days + 1;
        var savingsRate = totalIncome > 0 ? (totalIncome - totalExpense) / totalIncome * 100 : 0;

        var topExpenseCategories =
            await GetTopCategoriesAsync(userId, startDate, endDate, cancellationToken: cancellationToken);
        var topIncomeCategories = await GetTopCategoriesAsync(userId, startDate, endDate, 5, false, cancellationToken);

        var dailyTrend = await GetSpendingTrendsAsync(userId, startDate, endDate, "daily", cancellationToken);

        return new FinancialOverviewDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            NetBalance = totalIncome - totalExpense,
            SavingsRate = Math.Round(savingsRate, 2),
            AverageDailyIncome = days > 0 ? Math.Round(totalIncome / days, 2) : 0,
            AverageDailyExpense = days > 0 ? Math.Round(totalExpense / days, 2) : 0,
            TotalTransactions = transactionCount,
            IncomeTransactionCount = incomeCount,
            ExpenseTransactionCount = expenseCount,
            TopExpenseCategories = topExpenseCategories,
            TopIncomeCategories = topIncomeCategories,
            DailyTrend = dailyTrend
        };
    }

    /// <summary>
    ///     Returns spending and income trends grouped by day, week, or month
    ///     over the specified date range.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">The inclusive start of the date range (UTC).</param>
    /// <param name="endDate">The inclusive end of the date range (UTC).</param>
    /// <param name="groupBy">
    ///     Grouping granularity: <c>"daily"</c>, <c>"weekly"</c>, or <c>"monthly"</c>.
    ///     Defaults to <c>"monthly"</c> for unrecognised values.
    /// </param>
    public async Task<List<SpendingTrendDto>> GetSpendingTrendsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        string groupBy = "monthly", CancellationToken cancellationToken = default)
    {
        var projections = await _unitOfWork.Transactions.GetTrendProjectionsAsync(
            userId, startDate, endDate, cancellationToken);

        var trends = groupBy.ToLower() switch
        {
            "daily" => GroupByDay(projections, startDate, endDate),
            "weekly" => GroupByWeek(projections, startDate, endDate),
            "monthly" => GroupByMonth(projections, startDate, endDate),
            _ => GroupByMonth(projections, startDate, endDate)
        };

        return trends;
    }

    /// <summary>
    ///     Returns a breakdown of spending (or income) by category for the given period,
    ///     including the percentage contribution of each category to the total.
    ///     Results are ordered from highest to lowest amount.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">The inclusive start of the date range (UTC).</param>
    /// <param name="endDate">The inclusive end of the date range (UTC).</param>
    /// <param name="expenseOnly">
    ///     When <c>true</c> (default), analyses expenses only.
    ///     When <c>false</c>, analyses income.
    /// </param>
    public async Task<List<CategoryBreakdownDto>> GetCategoryBreakdownAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        bool expenseOnly = true, CancellationToken cancellationToken = default)
    {
        var transactionType = expenseOnly ? TransactionType.Expense : TransactionType.Income;

        var summary = await _unitOfWork.Transactions.GetCategorySpendSummaryAsync(
            userId, startDate, endDate, transactionType, cancellationToken);

        var total = summary.Sum(s => s.TotalAmount);

        return summary
            .Select(s => new CategoryBreakdownDto
            {
                CategoryId = s.CategoryId,
                CategoryName = s.CategoryName,
                CategoryIcon = s.CategoryIcon,
                CategoryColor = s.CategoryColor,
                TotalAmount = s.TotalAmount,
                Percentage = total > 0 ? Math.Round(s.TotalAmount / total * 100, 2) : 0,
                TransactionCount = s.TransactionCount
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();
    }

    /// <summary>
    ///     Returns a month-over-month income and expense comparison for the last
    ///     <paramref name="numberOfMonths" /> months, including percentage changes
    ///     between consecutive months.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="numberOfMonths">
    ///     How many months to include in the comparison, counting backwards from the current month.
    ///     Defaults to 6.
    /// </param>
    public async Task<List<MonthlyComparisonDto>> GetMonthlyComparisonAsync(
        Guid userId,
        int numberOfMonths = 6, CancellationToken cancellationToken = default)
    {
        var comparisons = new List<MonthlyComparisonDto>();
        var currentDate = _dateTimeProvider.UtcNow;

        for (var i = numberOfMonths - 1; i >= 0; i--)
        {
            var targetDate = currentDate.AddMonths(-i);
            var startDate = new DateTime(targetDate.Year, targetDate.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var income =
                await _unitOfWork.Transactions.GetTotalIncomeAsync(userId, startDate, endDate, cancellationToken);
            var expense =
                await _unitOfWork.Transactions.GetTotalExpenseAsync(userId, startDate, endDate, cancellationToken);
            var count = await _unitOfWork.Transactions.GetTransactionCountAsync(userId, startDate, endDate,
                cancellationToken);

            comparisons.Add(new MonthlyComparisonDto
            {
                Month = targetDate.Month,
                Year = targetDate.Year,
                Period = targetDate.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                TotalIncome = income,
                TotalExpense = expense,
                NetBalance = income - expense,
                TransactionCount = count
            });
        }

        // Calculate month-over-month changes
        for (var i = 1; i < comparisons.Count; i++)
        {
            var current = comparisons[i];
            var previous = comparisons[i - 1];

            current.IncomeChange = previous.TotalIncome > 0
                ? Math.Round((current.TotalIncome - previous.TotalIncome) / previous.TotalIncome * 100, 2)
                : 0;

            current.ExpenseChange = previous.TotalExpense > 0
                ? Math.Round((current.TotalExpense - previous.TotalExpense) / previous.TotalExpense * 100, 2)
                : 0;
        }

        return comparisons;
    }

    /// <summary>
    ///     Returns the performance of every budget for the given month and year,
    ///     comparing actual spend against the budgeted amount and indicating whether
    ///     spending is on track relative to how far through the month we currently are.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="month">The budget month to evaluate (1–12).</param>
    /// <param name="year">The budget year to evaluate.</param>
    public async Task<List<BudgetPerformanceDto>> GetBudgetPerformanceAsync(
        Guid userId,
        int month,
        int year, CancellationToken cancellationToken = default)
    {
        var budgets = await _unitOfWork.Budgets.GetByMonthYearAsync(userId, month, year, cancellationToken);
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var performance = new List<BudgetPerformanceDto>();

        foreach (var budget in budgets)
        {
            var actualSpent = await _unitOfWork.Transactions.GetActualSpentAsync(
                userId, budget.CategoryId, startDate, endDate, cancellationToken);
            var percentageUsed = budget.Amount > 0 ? actualSpent / budget.Amount * 100 : 0;

            var status = percentageUsed >= 100 ? "Exceeded" :
                percentageUsed >= 80 ? "Approaching" : "Under";

            var daysInMonth = DateTime.DaysInMonth(year, month);
            var today = _dateTimeProvider.UtcNow;
            var daysPassed = today.Month == month && today.Year == year
                ? today.Day
                : daysInMonth;
            var expectedPercentage = daysPassed / (double)daysInMonth * 100;

            var isOnTrack = percentageUsed <= (decimal)expectedPercentage;

            performance.Add(new BudgetPerformanceDto
            {
                BudgetId = budget.Id,
                CategoryId = budget.CategoryId,
                CategoryName = budget.Category?.Name ?? "Unknown",
                CategoryIcon = budget.Category?.Icon,
                BudgetAmount = budget.Amount,
                ActualSpent = actualSpent,
                Remaining = budget.Amount - actualSpent,
                PercentageUsed = Math.Round(percentageUsed, 2),
                Status = status,
                IsOnTrack = isOnTrack
            });
        }

        return performance.OrderByDescending(p => p.PercentageUsed).ToList();
    }

    /// <summary>
    ///     Returns the top spending (or income) categories for the given date range,
    ///     ordered by total amount descending.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="startDate">The inclusive start of the date range (UTC).</param>
    /// <param name="endDate">The inclusive end of the date range (UTC).</param>
    /// <param name="count">Maximum number of categories to return. Defaults to 5.</param>
    /// <param name="expenseOnly">
    ///     When <c>true</c> (default), ranks expense categories.
    ///     When <c>false</c>, ranks income categories.
    /// </param>
    public async Task<List<TopCategoryDto>> GetTopCategoriesAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int count = 5,
        bool expenseOnly = true, CancellationToken cancellationToken = default)
    {
        var transactionType = expenseOnly ? TransactionType.Expense : TransactionType.Income;

        var summary = await _unitOfWork.Transactions.GetCategorySpendSummaryAsync(
            userId, startDate, endDate, transactionType, cancellationToken);

        return summary
            .Select(s => new TopCategoryDto
            {
                CategoryId = s.CategoryId,
                CategoryName = s.CategoryName,
                CategoryIcon = s.CategoryIcon,
                CategoryColor = s.CategoryColor,
                TotalAmount = s.TotalAmount,
                TransactionCount = s.TransactionCount,
                AverageTransaction = s.TransactionCount > 0
                    ? Math.Round(s.TotalAmount / s.TransactionCount, 2)
                    : 0
            })
            .OrderByDescending(c => c.TotalAmount)
            .Take(count)
            .ToList();
    }

    #region Private Helper Methods

    private static List<SpendingTrendDto> GroupByDay(
        List<TransactionTrendProjection> transactions,
        DateTime startDate,
        DateTime endDate)
    {
        var trends = new List<SpendingTrendDto>();
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            var dayTransactions = transactions.Where(t => t.TransactionDate.Date == currentDate).ToList();

            trends.Add(new SpendingTrendDto
            {
                Date = currentDate,
                Period = currentDate.ToString("MMM dd", CultureInfo.InvariantCulture),
                TotalIncome =
                    dayTransactions.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount),
                TotalExpense = dayTransactions.Where(t => t.TransactionType == TransactionType.Expense)
                    .Sum(t => t.Amount),
                NetBalance =
                    dayTransactions.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount) -
                    dayTransactions.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount),
                TransactionCount = dayTransactions.Count
            });

            currentDate = currentDate.AddDays(1);
        }

        return trends;
    }

    private static List<SpendingTrendDto> GroupByWeek(
        List<TransactionTrendProjection> transactions,
        DateTime startDate,
        DateTime endDate)
    {
        var trends = new List<SpendingTrendDto>();
        var currentDate = startDate.Date;
        var weekNumber = 1;

        while (currentDate <= endDate)
        {
            var weekEnd = currentDate.AddDays(6);
            if (weekEnd > endDate) weekEnd = endDate;

            var weekTransactions = transactions
                .Where(t => t.TransactionDate.Date >= currentDate && t.TransactionDate.Date <= weekEnd)
                .ToList();

            trends.Add(new SpendingTrendDto
            {
                Date = currentDate,
                Period = $"Week {weekNumber}",
                TotalIncome = weekTransactions.Where(t => t.TransactionType == TransactionType.Income)
                    .Sum(t => t.Amount),
                TotalExpense = weekTransactions.Where(t => t.TransactionType == TransactionType.Expense)
                    .Sum(t => t.Amount),
                NetBalance =
                    weekTransactions.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount) -
                    weekTransactions.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount),
                TransactionCount = weekTransactions.Count
            });

            currentDate = weekEnd.AddDays(1);
            weekNumber++;
        }

        return trends;
    }

    private static List<SpendingTrendDto> GroupByMonth(
        List<TransactionTrendProjection> transactions,
        DateTime startDate,
        DateTime endDate)
    {
        var trends = new List<SpendingTrendDto>();
        var currentDate = new DateTime(startDate.Year, startDate.Month, 1);

        while (currentDate <= endDate)
        {
            var monthEnd = currentDate.AddMonths(1).AddDays(-1);
            if (monthEnd > endDate) monthEnd = endDate;

            var monthTransactions = transactions
                .Where(t => t.TransactionDate >= currentDate && t.TransactionDate <= monthEnd)
                .ToList();

            trends.Add(new SpendingTrendDto
            {
                Date = currentDate,
                Period = currentDate.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                TotalIncome = monthTransactions.Where(t => t.TransactionType == TransactionType.Income)
                    .Sum(t => t.Amount),
                TotalExpense = monthTransactions.Where(t => t.TransactionType == TransactionType.Expense)
                    .Sum(t => t.Amount),
                NetBalance = monthTransactions.Where(t => t.TransactionType == TransactionType.Income)
                                 .Sum(t => t.Amount) -
                             monthTransactions.Where(t => t.TransactionType == TransactionType.Expense)
                                 .Sum(t => t.Amount),
                TransactionCount = monthTransactions.Count
            });

            currentDate = currentDate.AddMonths(1);
        }

        return trends;
    }

    #endregion
}