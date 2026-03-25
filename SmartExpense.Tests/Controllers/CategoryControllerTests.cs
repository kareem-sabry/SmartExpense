using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartExpense.Api.Controllers;
using SmartExpense.Application.Dtos.Category;
using SmartExpense.Application.Interfaces;

namespace SmartExpense.Tests.Controllers;

public class CategoryControllerTests
{
    private readonly Mock<ICategoryService> _categoryServiceMock;
    private readonly CategoryController _sut;
    private readonly Guid _userId;

    public CategoryControllerTests()
    {
        _categoryServiceMock = new Mock<ICategoryService>();
        _sut = new CategoryController(_categoryServiceMock.Object);
        _userId = Guid.NewGuid();

        // Setup HttpContext with authenticated user
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
    public async Task GetAll_ShouldReturnOkWithCategories()
    {
        // Arrange
        var categories = new List<CategoryReadDto>
        {
            new() { Id = 1, Name = "Food" },
            new() { Id = 2, Name = "Transport" }
        };

        _categoryServiceMock
            .Setup(x => x.GetAllAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        // Act
        var result = await _sut.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedCategories = okResult.Value.Should().BeAssignableTo<List<CategoryReadDto>>().Subject;
        returnedCategories.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithCategory_WhenExists()
    {
        // Arrange
        var category = new CategoryReadDto
        {
            Id = 1,
            Name = "Food",
            Icon = "🍔"
        };

        _categoryServiceMock
            .Setup(x => x.GetByIdAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        // Act
        var result = await _sut.GetById(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedCategory = okResult.Value.Should().BeAssignableTo<CategoryReadDto>().Subject;
        returnedCategory.Name.Should().Be("Food");
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction_WhenValid()
    {
        // Arrange
        var dto = new CategoryCreateDto
        {
            Name = "New Category",
            Icon = "🎯"
        };

        var created = new CategoryReadDto
        {
            Id = 1,
            Name = "New Category",
            Icon = "🎯"
        };

        _categoryServiceMock
            .Setup(x => x.CreateAsync(dto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(CategoryController.GetById));
        createdResult.RouteValues!["id"].Should().Be(1);
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent_WhenSuccessful()
    {
        // Arrange
        _categoryServiceMock
            .Setup(x => x.DeleteAsync(1, _userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _categoryServiceMock.Verify(x => x.DeleteAsync(1, _userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}