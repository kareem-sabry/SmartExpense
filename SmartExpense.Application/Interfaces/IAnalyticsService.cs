using SmartExpense.Application.Dtos.Analytics;

namespace SmartExpense.Application.Interfaces;

public interface IAnalyticsService
{
    Task<FinancialOverviewDto> GetFinancialOverviewAsync(Guid userId, DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<List<SpendingTrendDto>> GetSpendingTrendsAsync(Guid userId, DateTime startDate, DateTime endDate,
        string groupBy = "monthly", CancellationToken cancellationToken = default);

    Task<List<CategoryBreakdownDto>> GetCategoryBreakdownAsync(Guid userId, DateTime startDate, DateTime endDate,
        bool expenseOnly = true, CancellationToken cancellationToken = default);

    Task<List<MonthlyComparisonDto>> GetMonthlyComparisonAsync(Guid userId, int numberOfMonths = 6,
        CancellationToken cancellationToken = default);

    Task<List<BudgetPerformanceDto>> GetBudgetPerformanceAsync(Guid userId, int month, int year,
        CancellationToken cancellationToken = default);

    Task<List<TopCategoryDto>> GetTopCategoriesAsync(Guid userId, DateTime startDate, DateTime endDate, int count = 5,
        bool expenseOnly = true, CancellationToken cancellationToken = default);
}