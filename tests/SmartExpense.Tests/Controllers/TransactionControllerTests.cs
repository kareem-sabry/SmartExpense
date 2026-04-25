using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartExpense.Api.Controllers;
using SmartExpense.Application.Dtos.Transaction;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Enums;
using SmartExpense.Core.Models;

namespace SmartExpense.Tests.Controllers;

public class TransactionControllerTests
{
    private readonly TransactionController _sut;
    private readonly Mock<ITransactionService> _transactionServiceMock;
    private readonly Guid _userId;

    public TransactionControllerTests()
    {
        _transactionServiceMock = new Mock<ITransactionService>();
        _sut = new TransactionController(_transactionServiceMock.Object);
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
    public async Task GetTransactions_ShouldReturnOkWithPagedResult()
    {
        // Arrange
        var parameters = new TransactionQueryParameters();
        var pagedResult = new PagedResult<TransactionReadDto>
        {
            Data = new List<TransactionReadDto>
            {
                new() { Id = 1, Description = "Lunch", Amount = 25.50m }
            },
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 1
        };

        _transactionServiceMock
            .Setup(x => x.GetPagedAsync(_userId, parameters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _sut.GetTransactions(parameters, It.IsAny<CancellationToken>());

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedData = okResult.Value.Should().BeAssignableTo<PagedResult<TransactionReadDto>>().Subject;
        returnedData.Data.Should().HaveCount(1);
        returnedData.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetById_ShouldReturnOkWithTransaction_WhenExists()
    {
        // Arrange
        var transaction = new TransactionReadDto
        {
            Id = 1,
            Description = "Lunch",
            Amount = 25.50m,
            TransactionType = TransactionType.Expense
        };

        _transactionServiceMock
            .Setup(x => x.GetByIdAsync(1, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var result = await _sut.GetById(1, It.IsAny<CancellationToken>());

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedTransaction = okResult.Value.Should().BeAssignableTo<TransactionReadDto>().Subject;
        returnedTransaction.Description.Should().Be("Lunch");
    }

    [Fact]
    public async Task GetSummary_ShouldReturnOkWithSummary()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1);
        var endDate = new DateTime(2025, 1, 31);

        var summary = new TransactionSummaryDto
        {
            TotalIncome = 5000m,
            TotalExpense = 2500m,
            NetBalance = 2500m,
            TransactionCount = 25
        };

        _transactionServiceMock
            .Setup(x => x.GetSummaryAsync(_userId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        // Act
        var result = await _sut.GetSummary(startDate, endDate);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedSummary = okResult.Value.Should().BeAssignableTo<TransactionSummaryDto>().Subject;
        returnedSummary.NetBalance.Should().Be(2500m);
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtAction_WhenValid()
    {
        // Arrange
        var dto = new TransactionCreateDto
        {
            CategoryId = 1,
            Description = "Lunch",
            Amount = 25.50m,
            TransactionType = TransactionType.Expense,
            TransactionDate = DateTime.UtcNow
        };

        var created = new TransactionReadDto
        {
            Id = 1,
            Description = "Lunch",
            Amount = 25.50m
        };

        _transactionServiceMock
            .Setup(x => x.CreateAsync(dto, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        // Act
        var result = await _sut.Create(dto);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(TransactionController.GetById));
        createdResult.RouteValues!["id"].Should().Be(1);
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent_WhenSuccessful()
    {
        // Arrange
        _transactionServiceMock
            .Setup(x => x.DeleteAsync(1, _userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Delete(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _transactionServiceMock.Verify(x => x.DeleteAsync(1, _userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}