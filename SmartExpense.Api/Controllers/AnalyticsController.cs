using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpense.Application.Dtos.Analytics;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Constants;

namespace SmartExpense.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(Roles = IdentityRoleConstants.User)]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>
    ///     Returns a high-level financial overview for the authenticated user
    ///     within the specified date range, including income, expenses, and net balance.
    /// </summary>
    /// <param name="startDate">Inclusive start date of the overview period.</param>
    /// <param name="endDate">Inclusive end date of the overview period.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A financial overview containing aggregated income, expense, and balance data.</returns>
    /// <response code="200">Overview calculated and returned successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(FinancialOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FinancialOverviewDto>> GetFinancialOverview(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var overview = await _analyticsService.GetFinancialOverviewAsync(userId, startDate, endDate);
        return Ok(overview);
    }

    /// <summary>
    ///     Returns spending trends for the authenticated user over the given date range,
    ///     grouped by the specified interval (e.g. daily, weekly, monthly).
    /// </summary>
    /// <param name="startDate">Inclusive start date of the trend period.</param>
    /// <param name="endDate">Inclusive end date of the trend period.</param>
    /// <param name="groupBy">Grouping interval for the trend data. Defaults to <c>monthly</c>.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A list of spending trend data points grouped by the specified interval.</returns>
    /// <response code="200">Spending trends retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("spending-trends")]
    [ProducesResponseType(typeof(List<SpendingTrendDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<SpendingTrendDto>>> GetSpendingTrends(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string groupBy = "monthly",
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var trends = await _analyticsService.GetSpendingTrendsAsync(userId, startDate, endDate, groupBy);
        return Ok(trends);
    }

    /// <summary>
    ///     Returns a breakdown of spending by category for the authenticated user
    ///     within the specified date range. Optionally restricted to expense transactions only.
    /// </summary>
    /// <param name="startDate">Inclusive start date of the breakdown period.</param>
    /// <param name="endDate">Inclusive end date of the breakdown period.</param>
    /// <param name="expenseOnly">When <c>true</c>, only expense transactions are included. Defaults to <c>true</c>.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A list of category breakdown entries with amounts and percentages.</returns>
    /// <response code="200">Category breakdown retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("category-breakdown")]
    [ProducesResponseType(typeof(List<CategoryBreakdownDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CategoryBreakdownDto>>> GetCategoryBreakdown(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] bool expenseOnly = true,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var breakdown = await _analyticsService.GetCategoryBreakdownAsync(userId, startDate, endDate, expenseOnly);
        return Ok(breakdown);
    }

    /// <summary>
    ///     Returns a month-over-month comparison of income and expenses
    ///     for the authenticated user over the specified number of recent months.
    /// </summary>
    /// <param name="numberOfMonths">Number of past months to include in the comparison. Defaults to 6.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A list of monthly comparison entries ordered from oldest to most recent.</returns>
    /// <response code="200">Monthly comparison data retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("monthly-comparison")]
    [ProducesResponseType(typeof(List<MonthlyComparisonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<MonthlyComparisonDto>>> GetMonthlyComparison(
        [FromQuery] int numberOfMonths = 6,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var comparison = await _analyticsService.GetMonthlyComparisonAsync(userId, numberOfMonths);
        return Ok(comparison);
    }

    /// <summary>
    ///     Returns budget performance data for the authenticated user for the specified month and year,
    ///     showing how actual spending compares against defined budget limits per category.
    /// </summary>
    /// <param name="month">The calendar month (1–12) to evaluate.</param>
    /// <param name="year">The calendar year to evaluate.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A list of budget performance entries showing budgeted vs actual amounts per category.</returns>
    /// <response code="200">Budget performance data retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("budget-performance")]
    [ProducesResponseType(typeof(List<BudgetPerformanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<BudgetPerformanceDto>>> GetBudgetPerformance(
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var performance = await _analyticsService.GetBudgetPerformanceAsync(userId, month, year);
        return Ok(performance);
    }

    /// <summary>
    ///     Returns the top spending categories for the authenticated user within the given date range,
    ///     ranked by total amount. Optionally restricted to expense transactions only.
    /// </summary>
    /// <param name="startDate">Inclusive start date of the ranking period.</param>
    /// <param name="endDate">Inclusive end date of the ranking period.</param>
    /// <param name="count">Maximum number of top categories to return. Defaults to 5.</param>
    /// <param name="expenseOnly">When <c>true</c>, only expense transactions are considered. Defaults to <c>true</c>.</param>
    /// <param name="cancellationToken">Token to cancel the request if the client disconnects.</param>
    /// <returns>A ranked list of top categories with their total amounts.</returns>
    /// <response code="200">Top categories retrieved successfully.</response>
    /// <response code="401">The request is missing or contains an invalid JWT.</response>
    [HttpGet("top-categories")]
    [ProducesResponseType(typeof(List<TopCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<TopCategoryDto>>> GetTopCategories(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] int count = 5,
        [FromQuery] bool expenseOnly = true,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var topCategories =
            await _analyticsService.GetTopCategoriesAsync(userId, startDate, endDate, count, expenseOnly);
        return Ok(topCategories);
    }
}