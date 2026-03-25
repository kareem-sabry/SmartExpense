using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartExpense.Api.Controllers;
using SmartExpense.Application.Dtos.Analytics;
using SmartExpense.Application.Interfaces;

namespace SmartExpense.Tests.Controllers;

public class AnalyticsControllerTests
{
    private readonly Mock<IAnalyticsService> _analyticsServiceMock;
    private readonly AnalyticsController _sut;
    private readonly Guid _userId;

    public AnalyticsControllerTests()
    {
        _analyticsServiceMock = new Mock<IAnalyticsService>();
        _sut = new AnalyticsController(_analyticsServiceMock.Object);
        _userId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString()),
            new(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetFinancialOverview_ShouldReturnOkWithOverview()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var overview = new FinancialOverviewDto
        {
            TotalIncome = 5000m,
            TotalExpense = 3500m,
            NetBalance = 1500m,
            SavingsRate = 30m
        };

        _analyticsServiceMock
            .Setup(x => x.GetFinancialOverviewAsync(_userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);

        // Act
        var result = await _sut.GetFinancialOverview(startDate, endDate);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedOverview = okResult.Value.Should().BeAssignableTo<FinancialOverviewDto>().Subject;
        returnedOverview.TotalIncome.Should().Be(5000m);
        returnedOverview.SavingsRate.Should().Be(30m);
    }

    [Fact]
    public async Task GetSpendingTrends_ShouldReturnOkWithTrends()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var trends = new List<SpendingTrendDto>
        {
            new()
            {
                Period = "Jan 2025",
                TotalIncome = 5000m,
                TotalExpense = 3500m
            }
        };

        _analyticsServiceMock
            .Setup(x => x.GetSpendingTrendsAsync(_userId, startDate, endDate, "monthly", It.IsAny<CancellationToken>()))
            .ReturnsAsync(trends);

        // Act
        var result = await _sut.GetSpendingTrends(startDate, endDate);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedTrends = okResult.Value.Should().BeAssignableTo<List<SpendingTrendDto>>().Subject;
        returnedTrends.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCategoryBreakdown_ShouldReturnOkWithBreakdown()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var breakdown = new List<CategoryBreakdownDto>
        {
            new()
            {
                CategoryName = "Food",
                TotalAmount = 1200m,
                Percentage = 34.29m
            }
        };

        _analyticsServiceMock
            .Setup(x => x.GetCategoryBreakdownAsync(_userId, startDate, endDate, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(breakdown);

        // Act
        var result = await _sut.GetCategoryBreakdown(startDate, endDate);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedBreakdown = okResult.Value.Should().BeAssignableTo<List<CategoryBreakdownDto>>().Subject;
        returnedBreakdown.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMonthlyComparison_ShouldReturnOkWithComparison()
    {
        // Arrange
        var comparison = new List<MonthlyComparisonDto>
        {
            new()
            {
                Period = "Jan 2025",
                TotalIncome = 5000m,
                IncomeChange = 10m
            }
        };

        _analyticsServiceMock
            .Setup(x => x.GetMonthlyComparisonAsync(_userId, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comparison);

        // Act
        var result = await _sut.GetMonthlyComparison();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedComparison = okResult.Value.Should().BeAssignableTo<List<MonthlyComparisonDto>>().Subject;
        returnedComparison.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBudgetPerformance_ShouldReturnOkWithPerformance()
    {
        // Arrange
        var performance = new List<BudgetPerformanceDto>
        {
            new()
            {
                CategoryName = "Food",
                BudgetAmount = 500m,
                ActualSpent = 425m,
                Status = "Approaching"
            }
        };

        _analyticsServiceMock
            .Setup(x => x.GetBudgetPerformanceAsync(_userId, 2, 2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync(performance);

        // Act
        var result = await _sut.GetBudgetPerformance(2, 2025);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedPerformance = okResult.Value.Should().BeAssignableTo<List<BudgetPerformanceDto>>().Subject;
        returnedPerformance.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTopCategories_ShouldReturnOkWithTopCategories()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var topCategories = new List<TopCategoryDto>
        {
            new()
            {
                CategoryName = "Food",
                TotalAmount = 1200m,
                TransactionCount = 15
            }
        };

        _analyticsServiceMock
            .Setup(x => x.GetTopCategoriesAsync(_userId, startDate, endDate, 5, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topCategories);

        // Act
        var result = await _sut.GetTopCategories(startDate, endDate);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedCategories = okResult.Value.Should().BeAssignableTo<List<TopCategoryDto>>().Subject;
        returnedCategories.Should().HaveCount(1);
    }
}