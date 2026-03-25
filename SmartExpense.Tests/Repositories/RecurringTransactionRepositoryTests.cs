using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Repositories;

namespace SmartExpense.Tests.Repositories;

public class RecurringTransactionRepositoryTests : IDisposable
{
    private readonly Category _category;
    private readonly AppDbContext _context;
    private readonly RecurringTransactionRepository _sut;
    private readonly Guid _userId;

    public RecurringTransactionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _sut = new RecurringTransactionRepository(_context);
        _userId = Guid.NewGuid();

        _category = new Category
        {
            Name = "Rent",
            Icon = "🏠",
            IsSystemCategory = false,
            UserId = _userId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        _context.Categories.Add(_category);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllForUserAsync_ShouldReturnUserRecurringTransactionsOnly()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();

        var userRecurring = new RecurringTransaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "User Recurring",
            Amount = 100m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var otherUserRecurring = new RecurringTransaction
        {
            UserId = otherUserId,
            CategoryId = _category.Id,
            Description = "Other User Recurring",
            Amount = 200m,
            Frequency = RecurrenceFrequency.Weekly,
            StartDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "other@example.com"
        };

        await _context.RecurringTransactions.AddRangeAsync(userRecurring, otherUserRecurring);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllForUserAsync(_userId);

        // Assert
        result.Should().HaveCount(1);
        result.First().UserId.Should().Be(_userId);
        result.First().Description.Should().Be("User Recurring");
    }

    [Fact]
    public async Task GetAllForUserAsync_ShouldFilterByActiveStatus()
    {
        // Arrange
        var activeRecurring = new RecurringTransaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Active",
            Amount = 100m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = DateTime.UtcNow,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var inactiveRecurring = new RecurringTransaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Inactive",
            Amount = 200m,
            Frequency = RecurrenceFrequency.Weekly,
            StartDate = DateTime.UtcNow,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.RecurringTransactions.AddRangeAsync(activeRecurring, inactiveRecurring);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllForUserAsync(_userId, true);

        // Assert
        result.Should().HaveCount(1);
        result.First().IsActive.Should().BeTrue();
        result.First().Description.Should().Be("Active");
    }

    [Fact]
    public async Task GetDueForGenerationAsync_ShouldReturnActiveRecurringWithinDateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var dueRecurring = new RecurringTransaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Due",
            Amount = 100m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(10),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var futureRecurring = new RecurringTransaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Future",
            Amount = 200m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = now.AddDays(30),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var expiredRecurring = new RecurringTransaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Expired",
            Amount = 300m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = now.AddDays(-60),
            EndDate = now.AddDays(-30),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.RecurringTransactions.AddRangeAsync(dueRecurring, futureRecurring, expiredRecurring);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetDueForGenerationAsync(now);

        // Assert
        result.Should().HaveCount(1);
        result.First().Description.Should().Be("Due");
    }

    [Fact]
    public async Task GetByIdForUserAsync_ShouldReturnRecurring_WhenExists()
    {
        // Arrange
        var recurring = new RecurringTransaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Test",
            Amount = 100m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.RecurringTransactions.AddAsync(recurring);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdForUserAsync(recurring.Id, _userId);

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be("Test");
    }
}