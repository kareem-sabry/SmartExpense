using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartExpense.Api.Controllers;
using SmartExpense.Application.Dtos.Budget;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Enums;

namespace SmartExpense.Tests.Controllers;

public class BudgetControllerTests
{
    private readonly Mock<IBudgetService> _budgetServiceMock;
    private readonly BudgetController _sut;
    private readonly Guid _userId;

    public BudgetControllerTests()
    {
        _budgetServiceMock = new Mock<IBudgetService>();
        _sut = new BudgetController(_budgetServiceMock.Object);
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
    public async Task GetAll_ShouldReturnOkWithBudgets()
    {
        // Arrange
        var budgets = new List<BudgetReadDto>
        {
            new()
            {
                Id = 1,
                CategoryName = "Food",
                Amount = 500m,
                Month = 2,
                Year = 2025,
                Status = BudgetStatus.UnderBudget
            }
        };

        _budgetServiceMock
            .Setup(x => x.GetAllAsync(_userId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budgets);

        // Act
        var result = await _sut.GetAll(null, null);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedBudgets = okResult.Value.Should().BeAssignableTo<List<BudgetReadDto>>().Subject;
        returnedBudgets.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithBudget_WhenExists()
    {
        // Arrange
        var budget = new BudgetReadDto
        {
            Id = 1,
            CategoryName = "Food",
            Amount = 500m,
            Month = 2,
            Year = 2025,
            Spent = 150m,
            Remaining = 350m,
            PercentageUsed = 30m,
            Status = BudgetStatus.UnderBudget
        };

        _budgetServiceMock
            .Setup(x => x.GetByIdAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        // Act
        var result = await _sut.GetById(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedBudget = okResult.Value.Should().BeAssignableTo<BudgetReadDto>().Subject;
        returnedBudget.CategoryName.Should().Be("Food");
        returnedBudget.PercentageUsed.Should().Be(30m);
    }

    [Fact]
    public async Task GetSummary_ShouldReturnOkWithSummary()
    {
        // Arrange
        var summary = new BudgetSummaryDto
        {
            Month = 2,
            Year = 2025,
            Period = "February 2025",
            TotalBudgeted = 1500m,
            TotalSpent = 800m,
            TotalRemaining = 700m,
            PercentageUsed = 53.33m,
            TotalBudgets = 3,
            BudgetsExceeded = 0,
            BudgetsApproaching = 1
        };

        _budgetServiceMock
            .Setup(x => x.GetSummaryAsync(_userId, 2, 2025, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        // Act
        var result = await _sut.GetSummary(2, 2025);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSummary = okResult.Value.Should().BeAssignableTo<BudgetSummaryDto>().Subject;
        returnedSummary.TotalBudgeted.Should().Be(1500m);
        returnedSummary.BudgetsApproaching.Should().Be(1);
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction_WhenValid()
    {
        // Arrange
        var dto = new BudgetCreateDto
        {
            CategoryId = 1,
            Amount = 500m,
            Month = 3,
            Year = 2025
        };

        var created = new BudgetReadDto
        {
            Id = 1,
            CategoryName = "Food",
            Amount = 500m,
            Month = 3,
            Year = 2025
        };

        _budgetServiceMock
            .Setup(x => x.CreateAsync(dto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(BudgetController.GetById));
        createdResult.RouteValues!["id"].Should().Be(1);
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent_WhenSuccessful()
    {
        // Arrange
        _budgetServiceMock
            .Setup(x => x.DeleteAsync(1, _userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _budgetServiceMock.Verify(x => x.DeleteAsync(1, _userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task GetSummary_ShouldReturnBadRequest_WhenMonthIsInvalid(int invalidMonth)
    {
        // Act
        var result = await _sut.GetSummary(invalidMonth, 2025);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _budgetServiceMock.Verify(
            x => x.GetSummaryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}