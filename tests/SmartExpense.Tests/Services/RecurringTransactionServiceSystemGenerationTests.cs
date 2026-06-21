using FluentAssertions;
using Moq;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Enums;
using SmartExpense.Infrastructure.Services;

namespace SmartExpense.Tests.Services;

public class RecurringTransactionServiceSystemGenerationTests
{
    // ── Mocks ────────────────────────────────────────────────────────────────

    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<IRecurringTransactionRepository> _recurringRepositoryMock;
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;

    // ── SUT ──────────────────────────────────────────────────────────────────

    private readonly RecurringTransactionService _sut;

    // ── Shared Test Data ─────────────────────────────────────────────────────

    // Fixed "now" so CalculateDueDates results are deterministic regardless of
    // when the test actually runs.
    private readonly DateTime _now = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    // ── Constructor ───────────────────────────────────────────────────────────

    public RecurringTransactionServiceSystemGenerationTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _recurringRepositoryMock = new Mock<IRecurringTransactionRepository>();
        _transactionRepositoryMock = new Mock<ITransactionRepository>();

        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(_now);
        _unitOfWorkMock.Setup(x => x.RecurringTransactions).Returns(_recurringRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.Transactions).Returns(_transactionRepositoryMock.Object);
        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _recurringRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);

        _sut = new RecurringTransactionService(_unitOfWorkMock.Object, _dateTimeProviderMock.Object);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAllDueAsync_WhenNoTemplatesAreDue_ReturnsAllZeroesWithoutCrashing()
    {
        // Arrange
        _recurringRepositoryMock
            .Setup(x => x.GetDueForGenerationAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecurringTransaction>());

        // Act
        var result = await _sut.GenerateAllDueAsync();

        // Assert
        result.TemplatesProcessed.Should().Be(0);
        result.TransactionsGenerated.Should().Be(0);
        result.FailedTemplates.Should().Be(0);
        result.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAllDueAsync_WhenOneTemplateFails_OtherTemplatesStillProcess()
    {
        // This test verifies the core isolation guarantee: one bad template cannot
        // abort the sweep for everyone else.
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        _recurringRepositoryMock
            .Setup(x => x.GetDueForGenerationAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecurringTransaction>
            {
                BuildDailyTemplate(id: 1, userId: userId1),
                BuildDailyTemplate(id: 2, userId: userId2)
            });

        // Template 1: the dedup check throws (simulates a transient DB error).
        _transactionRepositoryMock
            .Setup(x => x.ExistsForRecurringOnDateAsync(
                1, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated DB error for template 1"));

        // Template 2: no prior transaction — will be generated normally.
        _transactionRepositoryMock
            .Setup(x => x.ExistsForRecurringOnDateAsync(
                2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.GenerateAllDueAsync();

        // Assert
        result.TemplatesProcessed.Should().Be(2);
        result.FailedTemplates.Should().Be(1);
        result.Failures.Should().ContainSingle(f =>
            f.RecurringTransactionId == 1 && f.UserId == userId1);

        // The critical assertion: template 2 generated despite template 1 failing.
        result.TransactionsGenerated.Should().Be(1);
    }

    [Fact]
    public async Task GenerateAllDueAsync_WhenAllTemplatesSucceed_ReturnsCorrectTotals()
    {
        // Arrange
        _recurringRepositoryMock
            .Setup(x => x.GetDueForGenerationAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecurringTransaction>
            {
                BuildDailyTemplate(id: 1, userId: Guid.NewGuid()),
                BuildDailyTemplate(id: 2, userId: Guid.NewGuid()),
                BuildDailyTemplate(id: 3, userId: Guid.NewGuid())
            });

        // All dedup checks return false → all templates generate one transaction each.
        _transactionRepositoryMock
            .Setup(x => x.ExistsForRecurringOnDateAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.GenerateAllDueAsync();

        // Assert
        result.TemplatesProcessed.Should().Be(3);
        result.FailedTemplates.Should().Be(0);
        result.Failures.Should().BeEmpty();
        result.TransactionsGenerated.Should().Be(3); // 1 per daily template on _now.Date
    }

    [Fact]
    public async Task GenerateAllDueAsync_WhenAllTemplatesFail_CapturesAllFailuresWithoutThrowing()
    {
        // GenerateAllDueAsync must never throw even if every single template fails.
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        _recurringRepositoryMock
            .Setup(x => x.GetDueForGenerationAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecurringTransaction>
            {
                BuildDailyTemplate(id: 1, userId: userId1),
                BuildDailyTemplate(id: 2, userId: userId2)
            });

        _transactionRepositoryMock
            .Setup(x => x.ExistsForRecurringOnDateAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB unavailable"));

        // Act — must not throw
        var result = await _sut.GenerateAllDueAsync();

        // Assert
        result.TemplatesProcessed.Should().Be(2);
        result.FailedTemplates.Should().Be(2);
        result.TransactionsGenerated.Should().Be(0);
        result.Failures.Should().HaveCount(2);
        result.Failures.Should().Contain(f => f.RecurringTransactionId == 1 && f.UserId == userId1);
        result.Failures.Should().Contain(f => f.RecurringTransactionId == 2 && f.UserId == userId2);
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal Daily template where StartDate = _now.Date.
    /// CalculateDueDates with this setup produces exactly one due date:
    ///   lastGenerated  = _now.Date - 1 day  (because LastGeneratedDate is null)
    ///   currentDate    = lastGenerated + 1 day = _now.Date
    ///   _now.Date <= _now → add _now.Date
    ///   _now.Date + 1 day > _now → stop
    /// Result: one predictable due date, easy to assert on.
    /// </summary>
    private RecurringTransaction BuildDailyTemplate(int id, Guid userId) =>
        new()
        {
            Id = id,
            UserId = userId,
            CategoryId = 1,
            Category = new Category { Id = 1, Name = "Test", IsActive = true },
            Description = $"Daily Template {id}",
            Amount = 1000m,
            TransactionType = TransactionType.Expense,
            Frequency = RecurrenceFrequency.Daily,
            StartDate = _now.Date,
            IsActive = true,
            LastGeneratedDate = null
        };
}