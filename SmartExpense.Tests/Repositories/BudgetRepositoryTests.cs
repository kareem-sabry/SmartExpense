using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartExpense.Core.Entities;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Repositories;

namespace SmartExpense.Tests.Repositories;

public class BudgetRepositoryTests : IDisposable
{
    private readonly Category _category;
    private readonly AppDbContext _context;
    private readonly BudgetRepository _sut;
    private readonly Guid _userId;

    public BudgetRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _sut = new BudgetRepository(_context);
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
    public async Task GetAllForUserAsync_ShouldReturnUserBudgetsOnly()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();

        var userBudget = new Budget
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        var otherUserBudget = new Budget
        {
            UserId = otherUserId,
            CategoryId = _category.Id,
            Amount = 300m,
            Month = 2,
            Year = 2025,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "other@example.com"
        };

        await _context.Budgets.AddRangeAsync(userBudget, otherUserBudget);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllForUserAsync(_userId, null, null);

        // Assert
        result.Should().HaveCount(1);
        result.First().UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task GetAllForUserAsync_ShouldFilterByMonthAndYear()
    {
        // Arrange
        var budgets = new List<Budget>
        {
            new()
            {
                UserId = _userId,
                CategoryId = _category.Id,
                Amount = 500m,
                Month = 2,
                Year = 2025,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "test@example.com"
            },
            new()
            {
                UserId = _userId,
                CategoryId = _category.Id,
                Amount = 600m,
                Month = 3,
                Year = 2025,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "test@example.com"
            },
            new()
            {
                UserId = _userId,
                CategoryId = _category.Id,
                Amount = 400m,
                Month = 2,
                Year = 2024,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "test@example.com"
            }
        };

        await _context.Budgets.AddRangeAsync(budgets);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllForUserAsync(_userId, 2, 2025);

        // Assert
        result.Should().HaveCount(1);
        result.First().Month.Should().Be(2);
        result.First().Year.Should().Be(2025);
    }

    [Fact]
    public async Task GetByCategoryAndPeriodAsync_ShouldReturnBudget_WhenExists()
    {
        // Arrange
        var budget = new Budget
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.Budgets.AddAsync(budget);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByCategoryAndPeriodAsync(_userId, _category.Id, 2, 2025);

        // Assert
        result.Should().NotBeNull();
        result!.CategoryId.Should().Be(_category.Id);
        result.Month.Should().Be(2);
        result.Year.Should().Be(2025);
    }

    [Fact]
    public async Task BudgetExistsAsync_ShouldReturnTrue_WhenBudgetExists()
    {
        // Arrange
        var budget = new Budget
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.Budgets.AddAsync(budget);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.BudgetExistsAsync(_userId, _category.Id, 2, 2025);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task BudgetExistsAsync_ShouldExcludeSpecifiedBudgetId()
    {
        // Arrange
        var budget = new Budget
        {
            UserId = _userId,
            CategoryId = _category.Id,
            Amount = 500m,
            Month = 2,
            Year = 2025,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "test@example.com"
        };

        await _context.Budgets.AddAsync(budget);
        await _context.SaveChangesAsync();

        // Act -
        var result = await _sut.BudgetExistsAsync(_userId, _category.Id, 2, 2025, budget.Id);

        // Assert
        result.Should().BeFalse();
    }
}