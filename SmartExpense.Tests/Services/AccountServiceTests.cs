using FluentAssertions;
using Moq;
using SmartExpense.Application.Dtos.Category;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Exceptions;
using SmartExpense.Infrastructure.Services;

namespace SmartExpense.Tests.Services;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly DateTime _now;
    private readonly CategoryService _sut;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Guid _userId;

    public CategoryServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();

        _userId = Guid.NewGuid();
        _now = new DateTime(2025, 1, 31, 12, 0, 0, DateTimeKind.Utc);

        
        _unitOfWorkMock.Setup(x => x.Categories).Returns(_categoryRepositoryMock.Object);

        _sut = new CategoryService(_unitOfWorkMock.Object);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnSystemAndUserCategories()
    {
        // Arrange
        var categories = new List<Category>
        {
            new() { Id = 1, Name = "Food", IsSystemCategory = true, UserId = null },
            new() { Id = 2, Name = "Custom", IsSystemCategory = false, UserId = _userId }
        };

        _categoryRepositoryMock
            .Setup(x => x.GetAllForUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        // Act
        var result = await _sut.GetAllAsync(_userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "Food" && c.IsSystemCategory);
        result.Should().Contain(c => c.Name == "Custom" && !c.IsSystemCategory);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoCategories()
    {
        // Arrange
        _categoryRepositoryMock
            .Setup(x => x.GetAllForUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>());

        // Act
        var result = await _sut.GetAllAsync(_userId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCategory_WhenExists()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            Name = "Food",
            Icon = "🍔",
            Color = "#FF6B6B",
            IsSystemCategory = true
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        var result = await _sut.GetByIdAsync(1, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Food");
        result.Icon.Should().Be("🍔");
        result.IsSystemCategory.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // Act
        Func<Task> act = async () => await _sut.GetByIdAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Category with identifier '999' was not found.");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateCategory_WhenValid()
    {
        // Arrange
        var dto = new CategoryCreateDto
        {
            Name = "New Category",
            Icon = "🎯",
            Color = "#123456"
        };

        _categoryRepositoryMock
            .Setup(x => x.CategoryNameExistsAsync(_userId, dto.Name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _categoryRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c,CancellationToken cancellationToken) => c);

        // Act
        var result = await _sut.CreateAsync(dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Category");
        result.Icon.Should().Be("🎯");
        result.Color.Should().Be("#123456");
        result.IsSystemCategory.Should().BeFalse();

        _categoryRepositoryMock.Verify(x => x.AddAsync(It.Is<Category>(c =>
            c.Name == dto.Name &&
            c.UserId == _userId &&
            c.IsSystemCategory == false
        ), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowConflictException_WhenNameAlreadyExists()
    {
        // Arrange
        var dto = new CategoryCreateDto
        {
            Name = "Existing Category",
            Icon = "🎯"
        };

        _categoryRepositoryMock
            .Setup(x => x.CategoryNameExistsAsync(_userId, dto.Name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Func<Task> act = async () => await _sut.CreateAsync(dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Category with this name already exists");

        _categoryRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateCategory_WhenValid()
    {
        // Arrange
        var existingCategory = new Category
        {
            Id = 1,
            UserId = _userId,
            Name = "Old Name",
            Icon = "🎯",
            IsSystemCategory = false
        };

        var dto = new CategoryUpdateDto
        {
            Name = "Updated Name",
            Icon = "🔥",
            Color = "#FF0000",
            IsActive = true
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCategory);

        _categoryRepositoryMock
            .Setup(x => x.CategoryNameExistsAsync(_userId, dto.Name, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Icon.Should().Be("🔥");

        _categoryRepositoryMock.Verify(x => x.UpdateAsync(It.Is<Category>(c =>
            c.Id == 1 &&
            c.Name == dto.Name &&
            c.Icon == dto.Icon
        ), It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        var dto = new CategoryUpdateDto { Name = "Updated Name" };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(999, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowForbiddenException_WhenUpdatingSystemCategory()
    {
        // Arrange
        var systemCategory = new Category
        {
            Id = 1,
            Name = "Food",
            IsSystemCategory = true,
            UserId = null
        };

        var dto = new CategoryUpdateDto { Name = "Updated Name" };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemCategory);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Cannot update system categories");

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowConflictException_WhenNewNameAlreadyExists()
    {
        // Arrange
        var existingCategory = new Category
        {
            Id = 1,
            UserId = _userId,
            Name = "Category A",
            IsSystemCategory = false
        };

        var dto = new CategoryUpdateDto { Name = "Category B" };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCategory);

        _categoryRepositoryMock
            .Setup(x => x.CategoryNameExistsAsync(_userId, "Category B", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Func<Task> act = async () => await _sut.UpdateAsync(1, dto, _userId);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Category with this name already exists");

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeleteCategory_WhenValid()
    {
        // Arrange
        var category = new Category
        {
            Id = 1,
            UserId = _userId,
            Name = "To Delete",
            IsSystemCategory = false
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        await _sut.DeleteAsync(1, _userId);

        // Assert
        _categoryRepositoryMock.Verify(x => x.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowNotFoundException_WhenCategoryDoesNotExist()
    {
        // Arrange
        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(999, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // Act
        var act = async () => await _sut.DeleteAsync(999, _userId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _categoryRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowForbiddenException_WhenDeletingSystemCategory()
    {
        // Arrange
        var systemCategory = new Category
        {
            Id = 1,
            Name = "Food",
            IsSystemCategory = true
        };

        _categoryRepositoryMock
            .Setup(x => x.GetByIdForUserAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemCategory);

        // Act
        var act = async () => await _sut.DeleteAsync(1, _userId);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Cannot delete system categories");

        _categoryRepositoryMock.Verify(x => x.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}