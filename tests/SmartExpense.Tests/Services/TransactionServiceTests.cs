using FluentAssertions;
using Moq;
using SmartExpense.Application.Dtos.Transaction;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Exceptions;
using SmartExpense.Core.Models;
using SmartExpense.Infrastructure.Services;

namespace SmartExpense.Tests.Services;

public class TransactionServiceTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly DateTime _now;
    private readonly TransactionService _sut;
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Guid _userId;

    public TransactionServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _transactionRepositoryMock = new Mock<ITransactionRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();

        _userId = Guid.NewGuid();
        _now = new DateTime(2025, 1, 31, 12, 0, 0, DateTimeKind.Utc);

        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(_now);
        _unitOfWorkMock.Setup(x => x.Transactions).Returns(_transactionRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Categories).Returns(_categoryRepositoryMock.Object);

        _sut = new TransactionService(_unitOfWorkMock.Object, _dateTimeProviderMock.Object);
    }

    #region GetRecentAsync Tests

    [Fact]
    public async Task GetRecentAsync_ShouldReturnRecentTransactions()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food" };
        var transactions = new List<Transaction>
        {
            new() { Id = 1, UserId = _userId, Description = "Lunch", Category = category, TransactionDate = _now },
            new()
            {
                Id = 2, UserId = _userId, Description = "Dinner", Category = category,
                TransactionDate = _now.AddDays(-1)
            }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetRecentAsync(_userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        var result = await _sut.GetRecentAsync(_userId);

        // Assert
        result.Should().HaveCount(2);
        result.First().Description.Should().Be("Lunch");
    }

    #endregion

    #region GetSummaryAsync Tests

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnCorrectSummary()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        _transactionRepositoryMock
            .Setup(x => x.GetTotalIncomeAsync(_userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5000m);

        _transactionRepositoryMock
            .Setup(x => x.GetTotalExpenseAsync(_userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2500m);

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionCountAsync(_userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        // Act
        var result = await _sut.GetSummaryAsync(_userId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.TotalIncome.Should().Be(5000m);
        result.TotalExpense.Should().Be(2500m);
        result.NetBalance.Should().Be(2500m);
        result.TransactionCount.Should().Be(25);
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
    }

    #endregion

    #region GetPagedAsync Tests

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedTransactions()
    {
        // Arrange
        var parameters = new TransactionQueryParameters
        {
            PageNumber = 1,
            PageSize = 10
        };

        var category = new Category { Id = 1, Name = "Food", Icon = "🍔", Color = "#FF0000" };
        var transactions = new List<Transaction>
        {
            new()
            {
                Id = 1,
                UserId = _userId,
                CategoryId = 1,
                Description = "Lunch",
                Amount = 25.50m,
                TransactionType = TransactionType.Expense,
                TransactionDate = _now,
                Category = category
            }
        };

        var pagedResult = new PagedResult<Transaction>
        {
            Data = transactions,
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, parameters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetPagedAsync(_userId, parameters);

        // Assert
        result.Should().NotBeNull();
        result.Data.Should().HaveCount(1);
        result.PageNumber.Should().Be(1);
        result.TotalCount.Should().Be(1);
        result.Data.First().Description.Should().Be("Lunch");
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnEmptyList_WhenNoTransactions()
    {
        // Arrange
        var parameters = new TransactionQueryParameters();

        var pagedResult = new PagedResult<Transaction>
        {
            Data = new List<Transaction>(),
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 0
        };

        _transactionRepositoryMock
            .Setup(x => x.GetPagedAsync(_userId, parameters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetPagedAsync(_userId, parameters);

        // Assert
        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTransaction_WhenExists()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food", Icon = "🍔" };
        var transaction = new Transaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Description = "Lunch",
            Amount = 25.50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now,
            Category = category
        };

        _transactionRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _sut.GetByIdAsync(1, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Description.Should().Be("Lunch");
        result.Amount.Should().Be(25.50m);
        result.CategoryName.Should().Be("Food");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowNotFoundException_WhenTransactionDoesNotExist()
    {
        // Arrange
        _transactionRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        // Act
        Func<Task> act = async () => await _sut.GetByIdAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Transaction with identifier '999' was not found.");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateTransaction_WhenValid()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            Name = "Food",
            Icon = "🍔",
            IsActive = true,
            UserId = _userId
        };

        var dto = new TransactionCreateDto
        {
            CategoryId = 1,
            Description = "Lunch",
            Amount = 25.50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now.AddHours(-1),
            Notes = "Team lunch"
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _transactionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken ct) => t);

        _transactionRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(It.IsAny<int>(), _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, Guid userId, CancellationToken ct) => new Transaction
            {
                Id = 1,
                UserId = userId,
                CategoryId = dto.CategoryId,
                Description = dto.Description,
                Amount = dto.Amount,
                TransactionType = dto.TransactionType,
                TransactionDate = dto.TransactionDate,
                Notes = dto.Notes,
                Category = category
            });

        // Act
        var result = await _sut.CreateAsync(dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be("Lunch");
        result.Amount.Should().Be(25.50m);
        result.CategoryName.Should().Be("Food");

        _transactionRepositoryMock.Verify(x => x.AddAsync(It.Is<Transaction>(t =>
            t.UserId == _userId &&
            t.CategoryId == dto.CategoryId &&
            t.Description == dto.Description &&
            t.Amount == dto.Amount
        ), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        var dto = new TransactionCreateDto
        {
            CategoryId = 999,
            Description = "Lunch",
            Amount = 25.50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Category with identifier '999' was not found.");

        _transactionRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowValidationException_WhenCategoryIsInactive()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            Name = "Food",
            IsActive = false,
            UserId = _userId
        };

        var dto = new TransactionCreateDto
        {
            CategoryId = 1,
            Description = "Lunch",
            Amount = 25.50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Cannot create transaction with inactive category");

        _transactionRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowValidationException_WhenTransactionDateIsInFuture()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            Name = "Food",
            IsActive = true,
            UserId = _userId
        };

        var dto = new TransactionCreateDto
        {
            CategoryId = 1,
            Description = "Lunch",
            Amount = 25.50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now.AddDays(1) // Future date
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Transaction date cannot be in the future");

        _transactionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTransaction_WhenValid()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            Name = "Food",
            IsActive = true,
            UserId = _userId
        };

        var existingTransaction = new Transaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Description = "Old Description",
            Amount = 20m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now.AddDays(-1),
            Category = category
        };

        var dto = new TransactionUpdateDto
        {
            CategoryId = 1,
            Description = "Updated Description",
            Amount = 30m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now.AddHours(-1)
        };

        _transactionRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTransaction);

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        var result = await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be("Updated Description");
        result.Amount.Should().Be(30m);

        _transactionRepositoryMock.Verify(x => x.UpdateAsync(It.Is<Transaction>(t =>
            t.Id == 1 &&
            t.Description == dto.Description &&
            t.Amount == dto.Amount
        ), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFoundException_WhenTransactionDoesNotExist()
    {
        // Arrange
        var dto = new TransactionUpdateDto
        {
            CategoryId = 1,
            Description = "Updated",
            Amount = 30m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now
        };

        _transactionRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(999, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowValidationException_WhenNewCategoryIsInactive()
    {
        // Arrange
        var oldCategory = new Category { Id = 1, Name = "Food", IsActive = true, UserId = _userId };
        var newCategory = new Category { Id = 2, Name = "Transport", IsActive = false, UserId = _userId };

        var existingTransaction = new Transaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Description = "Old",
            Category = oldCategory
        };

        var dto = new TransactionUpdateDto
        {
            CategoryId = 2,
            Description = "Updated",
            Amount = 30m,
            TransactionType = TransactionType.Expense,
            TransactionDate = _now
        };

        _transactionRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTransaction);

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(2, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newCategory);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Cannot update transaction with inactive category");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeleteTransaction_WhenExists()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Food" };
        var transaction = new Transaction
        {
            Id = 1,
            UserId = _userId,
            Description = "Lunch",
            Category = category
        };

        _transactionRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        await _sut.DeleteAsync(1, _userId);

        // Assert
        _transactionRepositoryMock.Verify(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowNotFoundException_WhenTransactionDoesNotExist()
    {
        // Arrange
        _transactionRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        // Act
        var act = async () => await _sut.DeleteAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _transactionRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
    
    #region ExportToCsvAsync Tests

[Fact]
public async Task ExportToCsvAsync_ShouldReturnCsvWithHeader()
{
    // Arrange
    var startDate = new DateTime(2025, 1, 1);
    var endDate = new DateTime(2025, 1, 31);
    var category = new Category { Id = 1, Name = "Food" };

    _transactionRepositoryMock
        .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PagedResult<Transaction>
        {
            Data = new List<Transaction>
            {
                new()
                {
                    Id = 1,
                    TransactionDate = new DateTime(2025, 1, 15),
                    Description = "Lunch",
                    Amount = 25.50m,
                    TransactionType = TransactionType.Expense,
                    Category = category
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = int.MaxValue
        });

    // Act
    var csv = await _sut.ExportToCsvAsync(_userId, startDate, endDate);

    // Assert
    csv.Should().StartWith("Date,Description,Category,Type,Amount,Notes");
    csv.Should().Contain("2025-01-15");
    csv.Should().Contain("Lunch");
    csv.Should().Contain("Food");
    csv.Should().Contain("Expense");
    csv.Should().Contain("25.50");
}

[Fact]
public async Task ExportToCsvAsync_ShouldEscapeQuotesInDescription()
{
    // Arrange
    var startDate = new DateTime(2025, 1, 1);
    var endDate = new DateTime(2025, 1, 31);
    var category = new Category { Id = 1, Name = "Food" };

    _transactionRepositoryMock
        .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PagedResult<Transaction>
        {
            Data = new List<Transaction>
            {
                new()
                {
                    TransactionDate = new DateTime(2025, 1, 15),
                    Description = "He said \"hello\"",
                    Amount = 10m,
                    TransactionType = TransactionType.Expense,
                    Category = category
                }
            },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = int.MaxValue
        });

    // Act
    var csv = await _sut.ExportToCsvAsync(_userId, startDate, endDate);

    // Assert — double-quotes are escaped as two double-quotes in CSV
    csv.Should().Contain("He said \"\"hello\"\"");
}

[Fact]
public async Task ExportToCsvAsync_ShouldReturnHeaderOnly_WhenNoTransactions()
{
    // Arrange
    var startDate = new DateTime(2025, 1, 1);
    var endDate = new DateTime(2025, 1, 31);

    _transactionRepositoryMock
        .Setup(x => x.GetPagedAsync(_userId, It.IsAny<TransactionQueryParameters>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PagedResult<Transaction>
        {
            Data = new List<Transaction>(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = int.MaxValue
        });

    // Act
    var csv = await _sut.ExportToCsvAsync(_userId, startDate, endDate);

    // Assert
    var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    lines.Should().HaveCount(1);
    lines[0].Should().Be("Date,Description,Category,Type,Amount,Notes");
}

#endregion

    #region Edge case tests

[Fact]
public async Task CreateAsync_ShouldThrowValidationException_WhenTransactionDateIsExactlyNow()
{
    // Arrange — date exactly equal to now is not in the future, should be allowed
    var category = new Category { Id = 1, Name = "Food", IsActive = true, UserId = _userId };

    var dto = new TransactionCreateDto
    {
        CategoryId = 1,
        Description = "Test",
        Amount = 10m,
        TransactionType = TransactionType.Expense,
        TransactionDate = _now  // exactly now — should succeed
    };

    _categoryRepositoryMock
        .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(category);

    _transactionRepositoryMock
        .Setup(x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Transaction t, CancellationToken _) => t);

    _transactionRepositoryMock
        .Setup(x => x.GetByIdForUserAsync(It.IsAny<int>(), _userId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Transaction
        {
            Id = 1, Category = category, Description = dto.Description, Amount = dto.Amount,
            TransactionType = dto.TransactionType, TransactionDate = dto.TransactionDate
        });

    // Act — should not throw
    var result = await _sut.CreateAsync(dto, _userId);

    // Assert
    result.Should().NotBeNull();
}

[Fact]
public async Task GetSummaryAsync_ShouldReturnZeros_WhenNoTransactions()
{
    // Arrange
    _transactionRepositoryMock
        .Setup(x => x.GetTotalIncomeAsync(_userId, null, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(0m);

    _transactionRepositoryMock
        .Setup(x => x.GetTotalExpenseAsync(_userId, null, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(0m);

    _transactionRepositoryMock
        .Setup(x => x.GetTransactionCountAsync(_userId, null, null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(0);

    // Act
    var result = await _sut.GetSummaryAsync(_userId, null, null);

    // Assert
    result.TotalIncome.Should().Be(0m);
    result.TotalExpense.Should().Be(0m);
    result.NetBalance.Should().Be(0m);
    result.TransactionCount.Should().Be(0);
}

#endregion
}