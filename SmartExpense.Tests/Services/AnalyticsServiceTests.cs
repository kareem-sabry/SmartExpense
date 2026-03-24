using FluentAssertions;
using Moq;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Models;
using SmartExpense.Infrastructure.Services;

namespace SmartExpense.Tests.Services;

public class AnalyticsServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly Mock<IBudgetRepository> _budgetRepositoryMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly AnalyticsService _sut;
    private readonly Guid _userId;

    public AnalyticsServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionRepositoryMock = new Mock<ITransactionRepository>();
        _budgetRepositoryMock = new Mock<IBudgetRepository>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        
        _userId = Guid.NewGuid();
        _dateTimeProviderMock
            .Setup(x => x.UtcNow)
            .Returns(new DateTime(2025, 2, 15, 12, 0, 0, DateTimeKind.Utc));
        
        _unitOfWorkMock.Setup(x => x.Transactions).Returns(_transactionRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Budgets).Returns(_budgetRepositoryMock.Object);

        _sut = new AnalyticsService(_unitOfWorkMock.Object,_dateTimeProviderMock.Object);
    }

    #region GetFinancialOverviewAsync Tests

    [Fact]
    public async Task GetFinancialOverviewAsync_ShouldReturnCompleteOverview()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var category1 = new Category { Id = 1, Name = "Food", Icon = "🍔", Color = "#FF0000" };
        var category2 = new Category { Id = 2, Name = "Transport", Icon = "🚗", Color = "#00FF00" };

        var transactions = new List<Transaction>
        {
            new()
            {
                Id = 1,
                Amount = 1000m,
                TransactionType = TransactionType.Income,
                CategoryId = 1,
                Category = category1,
                TransactionDate = new DateTime(2025, 1, 15)
            },
            new()
            {
                Id = 2,
                Amount = 500m,
                TransactionType = TransactionType.Expense,
                CategoryId = 2,
                Category = category2,
                TransactionDate = new DateTime(2025, 1, 20)
            }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetTotalIncomeAsync(_userId, startDate, endDate))
            .ReturnsAsync(1000m);

        _transactionRepositoryMock
            .Setup(x => x.GetTotalExpenseAsync(_userId, startDate, endDate))
            .ReturnsAsync(500m);

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionCountAsync(_userId, startDate, endDate))
            .ReturnsAsync(2);

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction>
            {
                Data = transactions,
                TotalCount = 2
            });

        // Act
        var result = await _sut.GetFinancialOverviewAsync(_userId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.TotalIncome.Should().Be(1000m);
        result.TotalExpense.Should().Be(500m);
        result.NetBalance.Should().Be(500m);
        result.SavingsRate.Should().Be(50m);
        result.TotalTransactions.Should().Be(2);
        result.IncomeTransactionCount.Should().Be(1);
        result.ExpenseTransactionCount.Should().Be(1);
    }

    [Fact]
    public async Task GetFinancialOverviewAsync_ShouldCalculateSavingsRateCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        _transactionRepositoryMock
            .Setup(x => x.GetTotalIncomeAsync(_userId, startDate, endDate))
            .ReturnsAsync(5000m);

        _transactionRepositoryMock
            .Setup(x => x.GetTotalExpenseAsync(_userId, startDate, endDate))
            .ReturnsAsync(3500m);

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionCountAsync(_userId, startDate, endDate))
            .ReturnsAsync(10);

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = new List<Transaction>() });

        // Act
        var result = await _sut.GetFinancialOverviewAsync(_userId, startDate, endDate);

        // Assert
        result.SavingsRate.Should().Be(30m); // (5000 - 3500) / 5000 * 100 = 30%
    }

    #endregion

    #region GetSpendingTrendsAsync Tests

    [Fact]
    public async Task GetSpendingTrendsAsync_ShouldGroupByMonth()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 2, 28);

        var category = new Category { Id = 1, Name = "Food" };

        var transactions = new List<Transaction>
        {
            new()
            {
                Amount = 100m,
                TransactionType = TransactionType.Expense,
                TransactionDate = new DateTime(2025, 1, 15),
                Category = category
            },
            new()
            {
                Amount = 200m,
                TransactionType = TransactionType.Expense,
                TransactionDate = new DateTime(2025, 2, 15),
                Category = category
            }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        var result = await _sut.GetSpendingTrendsAsync(_userId, startDate, endDate, "monthly");

        // Assert
        result.Should().HaveCount(2);
        result[0].Period.Should().Be("Jan 2025");
        result[0].TotalExpense.Should().Be(100m);
        result[1].Period.Should().Be("Feb 2025");
        result[1].TotalExpense.Should().Be(200m);
    }

    [Fact]
    public async Task GetSpendingTrendsAsync_ShouldGroupByDay()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 3);

        var category = new Category { Id = 1, Name = "Food" };

        var transactions = new List<Transaction>
        {
            new()
            {
                Amount = 50m,
                TransactionType = TransactionType.Expense,
                TransactionDate = new DateTime(2025, 1, 1),
                Category = category
            },
            new()
            {
                Amount = 75m,
                TransactionType = TransactionType.Expense,
                TransactionDate = new DateTime(2025, 1, 2),
                Category = category
            }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        var result = await _sut.GetSpendingTrendsAsync(_userId, startDate, endDate, "daily");

        // Assert
        result.Should().HaveCount(3); // 3 days
        result[0].TotalExpense.Should().Be(50m);
        result[1].TotalExpense.Should().Be(75m);
        result[2].TotalExpense.Should().Be(0m); // No transactions on day 3
    }

    #endregion

    #region GetCategoryBreakdownAsync Tests

    [Fact]
    public async Task GetCategoryBreakdownAsync_ShouldCalculatePercentagesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var category1 = new Category { Id = 1, Name = "Food", Icon = "🍔" };
        var category2 = new Category { Id = 2, Name = "Transport", Icon = "🚗" };

        var transactions = new List<Transaction>
        {
            new()
            {
                Amount = 300m,
                TransactionType = TransactionType.Expense,
                CategoryId = 1,
                Category = category1
            },
            new()
            {
                Amount = 700m,
                TransactionType = TransactionType.Expense,
                CategoryId = 2,
                Category = category2
            }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        var result = await _sut.GetCategoryBreakdownAsync(_userId, startDate, endDate, expenseOnly: true);

        // Assert
        result.Should().HaveCount(2);
        result[0].CategoryName.Should().Be("Transport");
        result[0].TotalAmount.Should().Be(700m);
        result[0].Percentage.Should().Be(70m);
        result[1].CategoryName.Should().Be("Food");
        result[1].TotalAmount.Should().Be(300m);
        result[1].Percentage.Should().Be(30m);
    }

    [Fact]
    public async Task GetCategoryBreakdownAsync_ShouldFilterByExpenseOnly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var category = new Category { Id = 1, Name = "Salary" };

        var transactions = new List<Transaction>
        {
            new()
            {
                Amount = 1000m,
                TransactionType = TransactionType.Expense,
                CategoryId = 1,
                Category = category
            }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.Is<TransactionQueryParameters>(
                p => p.TransactionType == TransactionType.Expense)))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        await _sut.GetCategoryBreakdownAsync(_userId, startDate, endDate, expenseOnly: true);

        // Assert
        _transactionRepositoryMock.Verify(x => x.GetPagedAsync(_userId, It.Is<TransactionQueryParameters>(
            p => p.TransactionType == TransactionType.Expense)), Times.Once);
    }

    #endregion

    #region GetMonthlyComparisonAsync Tests

    [Fact]
    public async Task GetMonthlyComparisonAsync_ShouldReturnRequestedNumberOfMonths()
    {
        // Arrange
        _transactionRepositoryMock
            .Setup(x => x.GetTotalIncomeAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1000m);

        _transactionRepositoryMock
            .Setup(x => x.GetTotalExpenseAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(500m);

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionCountAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(10);

        // Act
        var result = await _sut.GetMonthlyComparisonAsync(_userId, numberOfMonths: 3);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetMonthlyComparisonAsync_ShouldCalculateMonthOverMonthChanges()
    {
        // Arrange
        var setupCount = 0;
        _transactionRepositoryMock
            .Setup(x => x.GetTotalIncomeAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(() =>
            {
                setupCount++;
                return setupCount == 1 ? 1000m : 1100m; // 10% increase
            });

        _transactionRepositoryMock
            .Setup(x => x.GetTotalExpenseAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(500m);

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionCountAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(10);

        // Act
        var result = await _sut.GetMonthlyComparisonAsync(_userId, numberOfMonths: 2);

        // Assert
        result.Should().HaveCount(2);
        result[1].IncomeChange.Should().Be(10m); // 10% increase from month 1 to month 2
    }

    #endregion

    #region GetBudgetPerformanceAsync Tests

    [Fact]
    public async Task GetBudgetPerformanceAsync_ShouldCalculatePerformanceCorrectly()
    {
        // Arrange
        var month = 2;
        var year = 2025;

        var category = new Category { Id = 1, Name = "Food", Icon = "🍔" };

        var budget = new Budget
        {
            Id = 1,
            CategoryId = 1,
            Amount = 500m,
            Month = month,
            Year = year,
            Category = category
        };

        var transactions = new List<Transaction>
        {
            new()
            {
                Amount = 425m,
                TransactionType = TransactionType.Expense,
                CategoryId = 1
            }
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByMonthYearAsync(_userId, month, year))
            .ReturnsAsync(new List<Budget> { budget });

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        var result = await _sut.GetBudgetPerformanceAsync(_userId, month, year);

        // Assert
        result.Should().HaveCount(1);
        result[0].BudgetAmount.Should().Be(500m);
        result[0].ActualSpent.Should().Be(425m);
        result[0].Remaining.Should().Be(75m);
        result[0].PercentageUsed.Should().Be(85m);
        result[0].Status.Should().Be("Approaching");
    }

    [Fact]
    public async Task GetBudgetPerformanceAsync_ShouldMarkAsExceeded_WhenOver100Percent()
    {
        // Arrange
        var month = 2;
        var year = 2025;

        var category = new Category { Id = 1, Name = "Food" };

        var budget = new Budget
        {
            Id = 1,
            CategoryId = 1,
            Amount = 500m,
            Month = month,
            Year = year,
            Category = category
        };

        var transactions = new List<Transaction>
        {
            new()
            {
                Amount = 600m,
                TransactionType = TransactionType.Expense,
                CategoryId = 1
            }
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByMonthYearAsync(_userId, month, year))
            .ReturnsAsync(new List<Budget> { budget });

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        var result = await _sut.GetBudgetPerformanceAsync(_userId, month, year);

        // Assert
        result[0].Status.Should().Be("Exceeded");
        result[0].PercentageUsed.Should().Be(120m);
    }

    #endregion

    #region GetTopCategoriesAsync Tests

    [Fact]
    public async Task GetTopCategoriesAsync_ShouldReturnTopCategoriesByAmount()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var category1 = new Category { Id = 1, Name = "Food", Icon = "🍔" };
        var category2 = new Category { Id = 2, Name = "Transport", Icon = "🚗" };
        var category3 = new Category { Id = 3, Name = "Shopping", Icon = "🛒" };

        var transactions = new List<Transaction>
        {
            new()
            {
                Amount = 100m,
                TransactionType = TransactionType.Expense,
                CategoryId = 1,
                Category = category1
            },
            new()
            {
                Amount = 300m,
                TransactionType = TransactionType.Expense,
                CategoryId = 2,
                Category = category2
            },
            new()
            {
                Amount = 200m,
                TransactionType = TransactionType.Expense,
                CategoryId = 3,
                Category = category3
            }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        var result = await _sut.GetTopCategoriesAsync(_userId, startDate, endDate, count: 3);

        // Assert
        result.Should().HaveCount(3);
        result[0].CategoryName.Should().Be("Transport");
        result[0].TotalAmount.Should().Be(300m);
        result[1].CategoryName.Should().Be("Shopping");
        result[1].TotalAmount.Should().Be(200m);
        result[2].CategoryName.Should().Be("Food");
        result[2].TotalAmount.Should().Be(100m);
    }

    [Fact]
    public async Task GetTopCategoriesAsync_ShouldLimitResults()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var category1 = new Category { Id = 1, Name = "Food" };
        var category2 = new Category { Id = 2, Name = "Transport" };
        var category3 = new Category { Id = 3, Name = "Shopping" };

        var transactions = new List<Transaction>
        {
            new() { Amount = 100m, TransactionType = TransactionType.Expense, CategoryId = 1, Category = category1 },
            new() { Amount = 200m, TransactionType = TransactionType.Expense, CategoryId = 2, Category = category2 },
            new() { Amount = 300m, TransactionType = TransactionType.Expense, CategoryId = 3, Category = category3 }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        var result = await _sut.GetTopCategoriesAsync(_userId, startDate, endDate, count: 2);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTopCategoriesAsync_ShouldCalculateAverageTransaction()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var category = new Category { Id = 1, Name = "Food" };

        var transactions = new List<Transaction>
        {
            new() { Amount = 50m, TransactionType = TransactionType.Expense, CategoryId = 1, Category = category },
            new() { Amount = 100m, TransactionType = TransactionType.Expense, CategoryId = 1, Category = category },
            new() { Amount = 150m, TransactionType = TransactionType.Expense, CategoryId = 1, Category = category }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = transactions });

        // Act
        var result = await _sut.GetTopCategoriesAsync(_userId, startDate, endDate, count: 5);

        // Assert
        result[0].TotalAmount.Should().Be(300m);
        result[0].TransactionCount.Should().Be(3);
        result[0].AverageTransaction.Should().Be(100m);
    }

    #endregion
}