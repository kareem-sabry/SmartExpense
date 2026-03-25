using FluentAssertions;
using Moq;
using SmartExpense.Application.Dtos.Budget;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Exceptions;
using SmartExpense.Core.Models;
using SmartExpense.Infrastructure.Services;

namespace SmartExpense.Tests.Services;

public class BudgetServiceTests
{
    private readonly Mock<IBudgetRepository> _budgetRepositoryMock;
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly DateTime _now;
    private readonly BudgetService _sut;
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Guid _userId;

    public BudgetServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _budgetRepositoryMock = new Mock<IBudgetRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _transactionRepositoryMock = new Mock<ITransactionRepository>();

        _userId = Guid.NewGuid();
        _now = new DateTime(2025, 2, 15, 12, 0, 0, DateTimeKind.Utc);

        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(_now);
        _unitOfWorkMock.Setup(x => x.Budgets).Returns(_budgetRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Categories).Returns(_categoryRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Transactions).Returns(_transactionRepositoryMock.Object);

        _sut = new BudgetService(_unitOfWorkMock.Object, _dateTimeProviderMock.Object);
    }

    #region GetSummaryAsync Tests

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnCorrectSummary()
    {
        // Arrange
        var category1 = new Category { Id = 1, Name = "Food" };
        var category2 = new Category { Id = 2, Name = "Transport" };

        var budgets = new List<Budget>
        {
            new()
            {
                Id = 1,
                UserId = _userId,
                CategoryId = 1,
                Amount = 500m,
                Month = 2,
                Year = 2025,
                Category = category1
            },
            new()
            {
                Id = 2,
                UserId = _userId,
                CategoryId = 2,
                Amount = 300m,
                Month = 2,
                Year = 2025,
                Category = category2
            }
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByMonthYearAsync(_userId, 2, 2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgets);

        // Setup different spending for each category
        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.Is<TransactionQueryParameters>(p => p.CategoryId == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction>
            {
                Data = new List<Transaction>
                {
                    new() { Amount = 450m, TransactionType = TransactionType.Expense } // 90% - Approaching
                }
            });

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.Is<TransactionQueryParameters>(p => p.CategoryId == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction>
            {
                Data = new List<Transaction>
                {
                    new() { Amount = 350m, TransactionType = TransactionType.Expense } // 116% - Exceeded
                }
            });

        // Act
        var result = await _sut.GetSummaryAsync(_userId, 2, 2025);

        // Assert
        result.Should().NotBeNull();
        result.Month.Should().Be(2);
        result.Year.Should().Be(2025);
        result.Period.Should().Be("February 2025");
        result.TotalBudgeted.Should().Be(800m);
        result.TotalSpent.Should().Be(800m); // 450 + 350
        result.TotalBudgets.Should().Be(2);
        result.BudgetsApproaching.Should().Be(1);
        result.BudgetsExceeded.Should().Be(1);
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnBudgets_WithCalculatedSpending()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food", Icon = "🍔", Color = "#FF0000" };
        var budgets = new List<Budget>
        {
            new()
            {
                Id = 1,
                UserId = _userId,
                CategoryId = 1,
                Amount = 500m,
                Month = 2,
                Year = 2025,
                Category = category
            }
        };

        _budgetRepositoryMock
            .Setup(x => x.GetAllForUserAsync(_userId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgets);

        // Setup transaction repository to return spent amount
        var pagedTransactions = new PagedResult<Transaction>
        {
            Data = new List<Transaction>
            {
                new()
                {
                    Id = 1,
                    Amount = 150m,
                    TransactionType = TransactionType.Expense,
                    CategoryId = 1
                }
            }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedTransactions);

        // Act
        var result = await _sut.GetAllAsync(_userId, null, null);

        // Assert
        result.Should().HaveCount(1);
        var budget = result.First();
        budget.Amount.Should().Be(500m);
        budget.Spent.Should().Be(150m);
        budget.Remaining.Should().Be(350m);
        budget.PercentageUsed.Should().Be(30m);
        budget.Status.Should().Be(BudgetStatus.UnderBudget);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoBudgets()
    {
        // Arrange
        _budgetRepositoryMock
            .Setup(x => x.GetAllForUserAsync(_userId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Budget>());

        // Act
        var result = await _sut.GetAllAsync(_userId, null, null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByMonthAndYear()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food" };
        var budgets = new List<Budget>
        {
            new()
            {
                Id = 1,
                UserId = _userId,
                CategoryId = 1,
                Amount = 500m,
                Month = 2,
                Year = 2025,
                Category = category
            }
        };

        _budgetRepositoryMock
            .Setup(x => x.GetAllForUserAsync(_userId, 2, 2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgets);

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = new List<Transaction>() });

        // Act
        var result = await _sut.GetAllAsync(_userId, 2, 2025);

        // Assert
        result.Should().HaveCount(1);
        _budgetRepositoryMock.Verify(x => x.GetAllForUserAsync(_userId, 2, 2025, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnBudget_WhenExists()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food", Icon = "🍔" };
        var budget = new Budget
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            Category = category
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = new List<Transaction>() });

        // Act
        var result = await _sut.GetByIdAsync(1, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Amount.Should().Be(500m);
        result.CategoryName.Should().Be("Food");
        result.Period.Should().Be("February 2025");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowNotFoundException_WhenBudgetDoesNotExist()
    {
        // Arrange
        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Budget?)null);

        // Act
        Func<Task> act = async () => await _sut.GetByIdAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Budget with identifier '999' was not found.");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateBudget_WhenValid()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            Name = "Food",
            UserId = _userId,
            IsActive = true
        };

        var dto = new BudgetCreateDto
        {
            CategoryId = 1,
            Amount = 500m,
            Month = 3, // Future month
            Year = 2025
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _budgetRepositoryMock
            .Setup(x => x.BudgetExistsAsync(_userId, 1, 3, 2025, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _budgetRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Budget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Budget b, CancellationToken cancellationToken) => b);

        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(It.IsAny<int>(), _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, Guid userId, CancellationToken ct) => new Budget
            {
                Id = 1,
                UserId = userId,
                CategoryId = dto.CategoryId,
                Amount = dto.Amount,
                Month = dto.Month,
                Year = dto.Year,
                Category = category
            });

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = new List<Transaction>() });

        // Act
        var result = await _sut.CreateAsync(dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(500m);
        result.Month.Should().Be(3);
        result.Year.Should().Be(2025);
        result.CategoryName.Should().Be("Food");

        _budgetRepositoryMock.Verify(x => x.AddAsync(It.Is<Budget>(b =>
            b.UserId == _userId &&
            b.CategoryId == dto.CategoryId &&
            b.Amount == dto.Amount &&
            b.Month == dto.Month &&
            b.Year == dto.Year
        ), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        var dto = new BudgetCreateDto
        {
            CategoryId = 999,
            Amount = 500m,
            Month = 3,
            Year = 2025
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Category with identifier '999' was not found.");

        _budgetRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Budget>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowConflictException_WhenBudgetAlreadyExists()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food", UserId = _userId };

        var dto = new BudgetCreateDto
        {
            CategoryId = 1,
            Amount = 500m,
            Month = 3,
            Year = 2025
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _budgetRepositoryMock
            .Setup(x => x.BudgetExistsAsync(
                _userId,
                1,
                3,
                2025,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Budget already exists

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Budget already exists for Food in March 2025");

        _budgetRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Budget>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowValidationException_WhenMonthIsInPast()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food", UserId = _userId };

        var dto = new BudgetCreateDto
        {
            CategoryId = 1,
            Amount = 500m,
            Month = 1, // January (past month, current is February)
            Year = 2025
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _budgetRepositoryMock
            .Setup(x => x.BudgetExistsAsync(
                _userId,
                1,
                3,
                2025,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Cannot create budget for past months");

        _budgetRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Budget>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateBudget_WhenValid()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food" };
        var existingBudget = new Budget
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            Category = category
        };

        var dto = new BudgetUpdateDto
        {
            Amount = 600m
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBudget);

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction> { Data = new List<Transaction>() });

        // Act
        var result = await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(600m);

        _budgetRepositoryMock.Verify(x => x.UpdateAsync(It.Is<Budget>(b =>
            b.Id == 1 &&
            b.Amount == 600m
        ), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFoundException_WhenBudgetDoesNotExist()
    {
        // Arrange
        var dto = new BudgetUpdateDto { Amount = 600m };

        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Budget?)null);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(999, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeleteBudget_WhenExists()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food" };
        var budget = new Budget
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Amount = 500m,
            Category = category
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        // Act
        await _sut.DeleteAsync(1, _userId);

        // Assert
        _budgetRepositoryMock.Verify(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowNotFoundException_WhenBudgetDoesNotExist()
    {
        // Arrange
        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Budget?)null);

        // Act
        var act = async () => await _sut.DeleteAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _budgetRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Budget Status Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnApproachingStatus_When80to99PercentSpent()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food" };
        var budget = new Budget
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            Category = category
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        // 85% spent
        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction>
            {
                Data = new List<Transaction>
                {
                    new() { Amount = 425m, TransactionType = TransactionType.Expense }
                }
            });

        // Act
        var result = await _sut.GetByIdAsync(1, _userId);

        // Assert
        result.Status.Should().Be(BudgetStatus.Approaching);
        result.StatusDisplay.Should().Be("Approaching");
        result.PercentageUsed.Should().Be(85m);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnExceededStatus_When100PercentOrMoreSpent()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food" };
        var budget = new Budget
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            Category = category
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        // 120% spent (exceeded)
        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction>
            {
                Data = new List<Transaction>
                {
                    new() { Amount = 600m, TransactionType = TransactionType.Expense }
                }
            });

        // Act
        var result = await _sut.GetByIdAsync(1, _userId);

        // Assert
        result.Status.Should().Be(BudgetStatus.Exceeded);
        result.StatusDisplay.Should().Be("Exceeded");
        result.PercentageUsed.Should().Be(120m);
        result.Remaining.Should().Be(-100m); // Negative remaining
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnUnderBudgetStatus_WhenLessThan80PercentSpent()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food" };
        var budget = new Budget
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            Category = category
        };

        _budgetRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        // 30% spent
        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Transaction>
            {
                Data = new List<Transaction>
                {
                    new() { Amount = 150m, TransactionType = TransactionType.Expense }
                }
            });

        // Act
        var result = await _sut.GetByIdAsync(1, _userId);

        // Assert
        result.Status.Should().Be(BudgetStatus.UnderBudget);
        result.StatusDisplay.Should().Be("UnderBudget");
        result.PercentageUsed.Should().Be(30m);
        result.Remaining.Should().Be(350m);
    }

    #endregion
}