using FluentAssertions;
using Moq;
using SmartExpense.Application.Dtos.RecurringTransaction;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Exceptions;
using SmartExpense.Infrastructure.Services;

namespace SmartExpense.Tests.Services;

public class RecurringTransactionServiceTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly DateTime _now;
    private readonly Mock<IRecurringTransactionRepository> _recurringRepositoryMock;
    private readonly RecurringTransactionService _sut;
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Guid _userId;

    public RecurringTransactionServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _recurringRepositoryMock = new Mock<IRecurringTransactionRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _transactionRepositoryMock = new Mock<ITransactionRepository>();

        _userId = Guid.NewGuid();
        _now = new DateTime(2025, 2, 15, 12, 0, 0, DateTimeKind.Utc);

        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(_now);
        _unitOfWorkMock.Setup(x => x.RecurringTransactions).Returns(_recurringRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Categories).Returns(_categoryRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Transactions).Returns(_transactionRepositoryMock.Object);

        _sut = new RecurringTransactionService(_unitOfWorkMock.Object, _dateTimeProviderMock.Object);
    }

    #region GenerateTransactionsAsync Tests

    [Fact]
    public async Task GenerateForRecurringTransactionAsync_ShouldRespectLoopCap_WhenMoreThan100DueDatesExist()
    {
        // Arrange
        // Daily frequency starting 2024-10-01 — 137 days before _now (2025-02-15).
        // Without a cap the loop would produce 137 transactions; the cap must stop it at 100.
        var category = new Category { Id = 1, Name = "Groceries" };
        var recurring = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Description = "Daily Groceries",
            Amount = 50m,
            Frequency = RecurrenceFrequency.Daily,
            StartDate = new DateTime(2024, 10, 1),
            LastGeneratedDate = null, // never generated → all dates are candidates
            IsActive = true,
            Category = category
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // No duplicates — every date should attempt to create a transaction until the cap fires.
        _transactionRepositoryMock
            .Setup(x => x.ExistsForRecurringOnDateAsync(1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _transactionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);

        // Act
        var result = await _sut.GenerateForRecurringTransactionAsync(1, _userId);

        // Assert — cap is 100, not 101
        result.TransactionsGenerated.Should().Be(100);
        _transactionRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Exactly(100));
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateForRecurringTransactionAsync_ShouldRespectEndDate_WhenEndDateBeforeAsOfDate()
    {
        // Arrange
        // Monthly frequency, StartDate = 2025-01-01, EndDate = 2025-01-31.
        // _now = 2025-02-15. The first (and only) due date within the valid range is 2025-01-31.
        // February's occurrence (2025-02-28) is after EndDate so must NOT be generated.
        var category = new Category { Id = 1, Name = "Rent" };
        var recurring = new RecurringTransaction
        {
            Id = 2,
            UserId = _userId,
            CategoryId = 1,
            Description = "Monthly Rent",
            Amount = 1500m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 1, 31),
            LastGeneratedDate = null,
            IsActive = true,
            Category = category
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(2, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        _transactionRepositoryMock
            .Setup(x => x.ExistsForRecurringOnDateAsync(2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _transactionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);

        // Act
        var result = await _sut.GenerateForRecurringTransactionAsync(2, _userId);

        // Assert — exactly one transaction (2025-01-31), none after EndDate
        result.TransactionsGenerated.Should().Be(1);
        _transactionRepositoryMock.Verify(
            x => x.AddAsync(
                It.Is<Transaction>(t => t.TransactionDate > new DateTime(2025, 1, 31)),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "No transactions should be generated after EndDate");
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateForRecurringTransactionAsync_ShouldUpdateLastGeneratedDate_EvenWhenAllDatesAreDeduped()
    {
        // Arrange
        // All candidate dates already exist in the DB (dedup returns true).
        // No new transactions are inserted, but LastGeneratedDate must still be updated
        // so the next run does not re-evaluate the same dates.
        var category = new Category { Id = 1, Name = "Salary" };
        var recurring = new RecurringTransaction
        {
            Id = 3,
            UserId = _userId,
            CategoryId = 1,
            Description = "Monthly Salary",
            Amount = 5000m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateTime(2025, 1, 1),
            LastGeneratedDate = null,
            IsActive = true,
            Category = category
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(3, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // All dates are already present — dedup fires for every candidate.
        _transactionRepositoryMock
            .Setup(x => x.ExistsForRecurringOnDateAsync(3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.GenerateForRecurringTransactionAsync(3, _userId);

        // Assert — no new transactions, but the template is still updated
        result.TransactionsGenerated.Should().Be(0);
        _transactionRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _recurringRepositoryMock.Verify(
            x => x.UpdateAsync(
                It.Is<RecurringTransaction>(r => r.LastGeneratedDate == _now),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "LastGeneratedDate must be updated so future runs do not re-evaluate the same dates");
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GenerateForRecurringTransactionAsync Tests

    [Fact]
    public async Task GenerateForRecurringTransactionAsync_ShouldThrowNotFoundException_WhenDoesNotExist()
    {
        // Arrange
        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecurringTransaction?)null);

        // Act
        Func<Task> act = async () => await _sut.GenerateForRecurringTransactionAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllRecurringTransactions()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Rent", Icon = "🏠" };
        var recurring = new List<RecurringTransaction>
        {
            new()
            {
                Id = 1,
                UserId = _userId,
                CategoryId = 1,
                Description = "Monthly Rent",
                Amount = 1500m,
                Frequency = RecurrenceFrequency.Monthly,
                StartDate = new DateTime(2025, 1, 1), // ⭐ ADD THIS
                IsActive = true,
                Category = category
            }
        };

        _recurringRepositoryMock
            .Setup(x => x.GetAllForUserAsync(_userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // Act
        var result = await _sut.GetAllAsync(_userId);

        // Assert
        result.Should().HaveCount(1);
        result.First().Description.Should().Be("Monthly Rent");
        result.First().FrequencyDisplay.Should().Be("Monthly");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByActiveStatus()
    {
        // Arrange
        var recurring = new List<RecurringTransaction>
        {
            new()
            {
                Id = 1,
                UserId = _userId,
                CategoryId = 1,
                Description = "Active",
                IsActive = true,
                StartDate = new DateTime(2025, 1, 1), // ⭐ ADD THIS
                Frequency = RecurrenceFrequency.Monthly, // ⭐ ADD THIS
                Category = new Category { Id = 1, Name = "Food" }
            }
        };

        _recurringRepositoryMock
            .Setup(x => x.GetAllForUserAsync(_userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // Act
        var result = await _sut.GetAllAsync(_userId, true);

        // Assert
        result.Should().HaveCount(1);
        result.First().IsActive.Should().BeTrue();
        _recurringRepositoryMock.Verify(x => x.GetAllForUserAsync(_userId, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnRecurringTransaction_WhenExists()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Rent" };
        var recurring = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Description = "Monthly Rent",
            Amount = 1500m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateTime(2025, 1, 1),
            IsActive = true,
            Category = category
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // Act
        var result = await _sut.GetByIdAsync(1, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be("Monthly Rent");
        result.Amount.Should().Be(1500m);
        result.FrequencyDisplay.Should().Be("Monthly");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowNotFoundException_WhenDoesNotExist()
    {
        // Arrange
        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecurringTransaction?)null);

        // Act
        Func<Task> act = async () => await _sut.GetByIdAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("RecurringTransaction with identifier '999' was not found.");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateRecurringTransaction_WhenValid()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            Name = "Rent",
            UserId = _userId,
            IsActive = true
        };

        var dto = new RecurringTransactionCreateDto
        {
            CategoryId = 1,
            Description = "Monthly Rent",
            Amount = 1500m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateTime(2025, 3, 1),
            Notes = "Apartment rent"
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _recurringRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecurringTransaction r, CancellationToken ct) => r);

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(It.IsAny<int>(), _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, Guid userId, CancellationToken ct) => new RecurringTransaction
            {
                Id = 1,
                UserId = userId,
                CategoryId = dto.CategoryId,
                Description = dto.Description,
                Amount = dto.Amount,
                TransactionType = dto.TransactionType,
                Frequency = dto.Frequency,
                StartDate = dto.StartDate,
                Notes = dto.Notes,
                IsActive = true,
                Category = category
            });

        // Act
        var result = await _sut.CreateAsync(dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be("Monthly Rent");
        result.Amount.Should().Be(1500m);
        result.FrequencyDisplay.Should().Be("Monthly");

        _recurringRepositoryMock.Verify(x => x.AddAsync(It.Is<RecurringTransaction>(r =>
            r.UserId == _userId &&
            r.CategoryId == dto.CategoryId &&
            r.Description == dto.Description &&
            r.IsActive == true
        ), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        var dto = new RecurringTransactionCreateDto
        {
            CategoryId = 999,
            Description = "Test",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = DateTime.UtcNow
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _recurringRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowValidationException_WhenCategoryIsInactive()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            Name = "Rent",
            IsActive = false,
            UserId = _userId
        };

        var dto = new RecurringTransactionCreateDto
        {
            CategoryId = 1,
            Description = "Test",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = DateTime.UtcNow
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Cannot create recurring transaction with inactive category");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowValidationException_WhenEndDateBeforeStartDate()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Rent", IsActive = true, UserId = _userId };

        var dto = new RecurringTransactionCreateDto
        {
            CategoryId = 1,
            Description = "Test",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateTime(2025, 3, 1),
            EndDate = new DateTime(2025, 2, 1) // Before start date
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("End date cannot be before start date");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateRecurringTransaction_WhenValid()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Rent", IsActive = true, UserId = _userId };
        var existing = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            Description = "Old Description",
            Amount = 1000m,
            StartDate = new DateTime(2025, 1, 1),
            Category = category
        };

        var dto = new RecurringTransactionUpdateDto
        {
            CategoryId = 1,
            Description = "Updated Description",
            Amount = 1500m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        var result = await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be("Updated Description");
        result.Amount.Should().Be(1500m);

        _recurringRepositoryMock.Verify(x => x.UpdateAsync(It.Is<RecurringTransaction>(r =>
            r.Id == 1 &&
            r.Description == dto.Description &&
            r.Amount == dto.Amount
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFoundException_WhenDoesNotExist()
    {
        // Arrange
        var dto = new RecurringTransactionUpdateDto
        {
            CategoryId = 1,
            Description = "Test",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecurringTransaction?)null);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(999, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        var existing = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            StartDate = new DateTime(2025, 1, 1),
            Category = new Category { Id = 1, Name = "Rent" }
        };

        var dto = new RecurringTransactionUpdateDto
        {
            CategoryId = 999,
            Description = "Test",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _recurringRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowValidationException_WhenCategoryIsInactive()
    {
        // Arrange
        var inactiveCategory = new Category { Id = 1, Name = "Rent", IsActive = false, UserId = _userId };
        var existing = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            StartDate = new DateTime(2025, 1, 1),
            Category = inactiveCategory
        };

        var dto = new RecurringTransactionUpdateDto
        {
            CategoryId = 1,
            Description = "Test",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveCategory);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Cannot update recurring transaction with inactive category");
        _recurringRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowValidationException_WhenEndDateBeforeStartDate()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Rent", IsActive = true, UserId = _userId };
        var existing = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            CategoryId = 1,
            StartDate = new DateTime(2025, 3, 1),
            Category = category
        };

        var dto = new RecurringTransactionUpdateDto
        {
            CategoryId = 1,
            Description = "Test",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            EndDate = new DateTime(2025, 2, 1) // Before existing StartDate
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("End date must be after the recurring transaction's start date.");
        _recurringRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeleteRecurringTransaction_WhenExists()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Rent" };
        var recurring = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            Description = "Monthly Rent",
            Category = category
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // Act
        await _sut.DeleteAsync(1, _userId);

        // Assert
        _recurringRepositoryMock.Verify(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowNotFoundException_WhenDoesNotExist()
    {
        // Arrange
        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecurringTransaction?)null);

        // Act
        var act = async () => await _sut.DeleteAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _recurringRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ToggleActiveAsync Tests

    [Fact]
    public async Task ToggleActiveAsync_ShouldToggleFromActiveToInactive()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Rent" };
        var recurring = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            Description = "Monthly Rent",
            IsActive = true,
            Category = category
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // Act
        var result = await _sut.ToggleActiveAsync(1, _userId);

        // Assert
        result.IsActive.Should().BeFalse();
        _recurringRepositoryMock.Verify(x => x.UpdateAsync(It.Is<RecurringTransaction>(r =>
            r.Id == 1 &&
            r.IsActive == false
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleActiveAsync_ShouldToggleFromInactiveToActive()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Rent" };
        var recurring = new RecurringTransaction
        {
            Id = 1,
            UserId = _userId,
            Description = "Monthly Rent",
            IsActive = false,
            Category = category
        };

        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // Act
        var result = await _sut.ToggleActiveAsync(1, _userId);

        // Assert
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleActiveAsync_ShouldThrowNotFoundException_WhenDoesNotExist()
    {
        // Arrange
        _recurringRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecurringTransaction?)null);

        // Act
        Func<Task> act = async () => await _sut.ToggleActiveAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _recurringRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}