using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartExpense.Core.Entities;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Repositories;

namespace SmartExpense.Tests.Repositories;

public class CategoryRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CategoryRepository _sut;
    private readonly Guid _userId;

    public CategoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _sut = new CategoryRepository(_context);
        _userId = Guid.NewGuid();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllForUserAsync_ShouldReturnSystemAndUserCategories()
    {
        // Arrange
        var systemCategory = new Category
        {
            Name = "Food",
            IsSystemCategory = true,
            UserId = null,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "System"
        };

        var userCategory = new Category
        {
            Name = "Custom",
            IsSystemCategory = false,
            UserId = _userId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "user@test.com"
        };

        var otherUserCategory = new Category
        {
            Name = "Other User Category",
            IsSystemCategory = false,
            UserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "other@test.com"
        };

        await _context.Categories.AddRangeAsync(systemCategory, userCategory, otherUserCategory);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllForUserAsync(_userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "Food" && c.IsSystemCategory);
        result.Should().Contain(c => c.Name == "Custom" && c.UserId == _userId);
        result.Should().NotContain(c => c.Name == "Other User Category");
    }

    [Fact]
    public async Task CategoryNameExistsAsync_ShouldReturnTrue_WhenNameExists()
    {
        // Arrange
        var category = new Category
        {
            Name = "Existing Category",
            UserId = _userId,
            IsSystemCategory = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "user@test.com"
        };

        await _context.Categories.AddAsync(category);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CategoryNameExistsAsync(_userId, "Existing Category");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CategoryNameExistsAsync_ShouldReturnFalse_WhenNameDoesNotExist()
    {
        // Arrange
        // No categories added

        // Act
        var result = await _sut.CategoryNameExistsAsync(_userId, "Non-Existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CategoryNameExistsAsync_ShouldExcludeSpecifiedId()
    {
        // Arrange
        var category1 = new Category
        {
            Name = "Category A",
            UserId = _userId,
            IsSystemCategory = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "user@test.com"
        };

        await _context.Categories.AddAsync(category1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.CategoryNameExistsAsync(_userId, "Category A", category1.Id);

        // Assert
        result.Should().BeFalse(); // Should return false because we're excluding the only match
    }
}