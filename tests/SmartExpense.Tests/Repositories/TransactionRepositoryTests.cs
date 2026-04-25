using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Models;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Repositories;

namespace SmartExpense.Tests.Repositories;

public class TransactionRepositoryTests : IDisposable
{
    private readonly Category _category;
    private readonly AppDbContext _context;
    private readonly TransactionRepository _sut;
    private readonly Guid _userId;

    public TransactionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _sut = new TransactionRepository(_context);
        _userId = Guid.NewGuid();

        _category = new Category
        {
            Name = "Food",
            Icon = "🍔",
            Color = "#FF0000",
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
    public async Task GetPagedAsync_ShouldReturnUserTransactionsOnly()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();

        var userTransaction = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "User Transaction",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            TransactionDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var otherTransaction = new Transaction
        {
            UserId = otherUserId,
            CategoryId = _category.Id,
            Description = "Other User Transaction",
            Amount = 200m,
            TransactionType = TransactionType.Expense,
            TransactionDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "other@example.com"
        };

        await _context.Transactions.AddRangeAsync(userTransaction, otherTransaction);
        await _context.SaveChangesAsync();

        var parameters = new TransactionQueryParameters
        {
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var result = await _sut.GetPagedAsync(_userId, parameters);

        // Assert
        result.Data.Should().HaveCount(1);
        result.Data.First().Description.Should().Be("User Transaction");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldFilterByCategory()
    {
        // Arrange
        var category2 = new Category
        {
            Name = "Transport",
            UserId = _userId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };
        await _context.Categories.AddAsync(category2);
        await _context.SaveChangesAsync();

        var foodTransaction = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Food",
            Amount = 50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var transportTransaction = new Transaction
        {
            UserId = _userId,
            CategoryId = category2.Id,
            Description = "Transport",
            Amount = 30m,
            TransactionType = TransactionType.Expense,
            TransactionDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.Transactions.AddRangeAsync(foodTransaction, transportTransaction);
        await _context.SaveChangesAsync();

        var parameters = new TransactionQueryParameters
        {
            CategoryId = _category.Id
        };

        // Act
        var result = await _sut.GetPagedAsync(_userId, parameters);

        // Assert
        result.Data.Should().HaveCount(1);
        result.Data.First().Description.Should().Be("Food");
    }

    [Fact]
    public async Task GetPagedAsync_ShouldFilterByTransactionType()
    {
        // Arrange
        var expense = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Expense",
            Amount = 50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var income = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Income",
            Amount = 1000m,
            TransactionType = TransactionType.Income,
            TransactionDate = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.Transactions.AddRangeAsync(expense, income);
        await _context.SaveChangesAsync();

        var parameters = new TransactionQueryParameters
        {
            TransactionType = TransactionType.Income
        };

        // Act
        var result = await _sut.GetPagedAsync(_userId, parameters);

        // Assert
        result.Data.Should().HaveCount(1);
        result.Data.First().Description.Should().Be("Income");
        result.Data.First().TransactionType.Should().Be(TransactionType.Income);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldFilterByDateRange()
    {
        // Arrange
        var oldTransaction = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Old",
            Amount = 50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = new DateTime(2024, 12, 1),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var newTransaction = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "New",
            Amount = 100m,
            TransactionType = TransactionType.Expense,
            TransactionDate = new DateTime(2025, 1, 15),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.Transactions.AddRangeAsync(oldTransaction, newTransaction);
        await _context.SaveChangesAsync();

        var parameters = new TransactionQueryParameters
        {
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 1, 31)
        };

        // Act
        var result = await _sut.GetPagedAsync(_userId, parameters);

        // Assert
        result.Data.Should().HaveCount(1);
        result.Data.First().Description.Should().Be("New");
    }

    [Fact]
    public async Task GetRecentAsync_ShouldReturnMostRecentTransactions()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new()
            {
                UserId = _userId,
                CategoryId = _category.Id,
                Description = "Transaction 1",
                Amount = 10m,
                TransactionType = TransactionType.Expense,
                TransactionDate = DateTime.UtcNow.AddDays(-3),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
                CreatedBy = "test@example.com"
            },
            new()
            {
                UserId = _userId,
                CategoryId = _category.Id,
                Description = "Transaction 2",
                Amount = 20m,
                TransactionType = TransactionType.Expense,
                TransactionDate = DateTime.UtcNow.AddDays(-1),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "test@example.com"
            },
            new()
            {
                UserId = _userId,
                CategoryId = _category.Id,
                Description = "Transaction 3",
                Amount = 30m,
                TransactionType = TransactionType.Expense,
                TransactionDate = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "test@example.com"
            }
        };

        await _context.Transactions.AddRangeAsync(transactions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetRecentAsync(_userId, 2);

        // Assert
        result.Should().HaveCount(2);
        result.First().Description.Should().Be("Transaction 3"); // Most recent
        result.Last().Description.Should().Be("Transaction 2");
    }

    [Fact]
    public async Task GetTotalIncomeAsync_ShouldCalculateCorrectTotal()
    {
        // Arrange
        var income1 = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Income 1",
            Amount = 1000m,
            TransactionType = TransactionType.Income,
            TransactionDate = new DateTime(2025, 1, 15),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var income2 = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Income 2",
            Amount = 500m,
            TransactionType = TransactionType.Income,
            TransactionDate = new DateTime(2025, 1, 20),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var expense = new Transaction
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Description = "Expense",
            Amount = 200m,
            TransactionType = TransactionType.Expense,
            TransactionDate = new DateTime(2025, 1, 18),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.Transactions.AddRangeAsync(income1, income2, expense);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetTotalIncomeAsync(
            _userId,
            new DateTime(2025, 1, 1),
            new DateTime(2025, 1, 31)
        );

        // Assert
        result.Should().Be(1500m); // Only income transactions
    }
}