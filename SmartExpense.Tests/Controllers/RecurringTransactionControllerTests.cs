using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartExpense.Api.Controllers;
using SmartExpense.Application.Dtos.RecurringTransaction;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Enums;

namespace SmartExpense.Tests.Controllers;

public class RecurringTransactionControllerTests
{
    private readonly Mock<IRecurringTransactionService> _recurringServiceMock;
    private readonly RecurringTransactionController _sut;
    private readonly Guid _userId;

    public RecurringTransactionControllerTests()
    {
        _recurringServiceMock = new Mock<IRecurringTransactionService>();
        _sut = new RecurringTransactionController(_recurringServiceMock.Object);
        _userId = Guid.NewGuid();

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
    public async Task GetAll_ShouldReturnOkWithRecurringTransactions()
    {
        // Arrange
        var recurring = new List<RecurringTransactionReadDto>
        {
            new()
            {
                Id = 1,
                Description = "Monthly Rent",
                Amount = 1500m,
                FrequencyDisplay = "Monthly"
            }
        };

        _recurringServiceMock
            .Setup(x => x.GetAllAsync(_userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // Act
        var result = await _sut.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedRecurring = okResult.Value.Should().BeAssignableTo<List<RecurringTransactionReadDto>>().Subject;
        returnedRecurring.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithRecurring_WhenExists()
    {
        // Arrange
        var recurring = new RecurringTransactionReadDto
        {
            Id = 1,
            Description = "Monthly Rent",
            Amount = 1500m,
            FrequencyDisplay = "Monthly"
        };

        _recurringServiceMock
            .Setup(x => x.GetByIdAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recurring);

        // Act
        var result = await _sut.GetById(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedRecurring = okResult.Value.Should().BeAssignableTo<RecurringTransactionReadDto>().Subject;
        returnedRecurring.Description.Should().Be("Monthly Rent");
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction_WhenValid()
    {
        // Arrange
        var dto = new RecurringTransactionCreateDto
        {
            CategoryId = 1,
            Description = "Monthly Rent",
            Amount = 1500m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = DateTime.UtcNow
        };

        var created = new RecurringTransactionReadDto
        {
            Id = 1,
            Description = "Monthly Rent"
        };

        _recurringServiceMock
            .Setup(x => x.CreateAsync(dto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(RecurringTransactionController.GetById));
    }

    [Fact]
    public async Task ToggleActive_ShouldReturnOkWithToggledRecurring()
    {
        // Arrange
        var toggled = new RecurringTransactionReadDto
        {
            Id = 1,
            Description = "Monthly Rent",
            IsActive = false
        };

        _recurringServiceMock
            .Setup(x => x.ToggleActiveAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toggled);

        // Act
        var result = await _sut.ToggleActive(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedRecurring = okResult.Value.Should().BeAssignableTo<RecurringTransactionReadDto>().Subject;
        returnedRecurring.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateTransactions_ShouldReturnOkWithResult()
    {
        // Arrange
        var generateResult = new GenerateTransactionsResultDto
        {
            TransactionsGenerated = 3,
            GeneratedTransactions = new List<GeneratedTransactionInfo>
            {
                new() { RecurringTransactionId = 1, Description = "Rent", Amount = 1500m }
            }
        };

        _recurringServiceMock
            .Setup(x => x.GenerateTransactionsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(generateResult);

        // Act
        var result = await _sut.GenerateTransactions();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedResult = okResult.Value.Should().BeAssignableTo<GenerateTransactionsResultDto>().Subject;
        returnedResult.TransactionsGenerated.Should().Be(3);
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent_WhenSuccessful()
    {
        // Arrange
        _recurringServiceMock
            .Setup(x => x.DeleteAsync(1, _userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }
}