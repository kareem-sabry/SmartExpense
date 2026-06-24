using System.Diagnostics;
using Quartz;
using SmartExpense.Application.Interfaces;

namespace SmartExpense.Worker.Jobs;

[DisallowConcurrentExecution]
public class RecurringTransactionGenerationJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringTransactionGenerationJob> _logger;

    public RecurringTransactionGenerationJob(IServiceScopeFactory scopeFactory,
        ILogger<RecurringTransactionGenerationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "Recurring transaction generation started. " +
            "ScheduledFireTime: {ScheduledFireTime:O}, ActualFireTime: {ActualFireTime:O}",
            context.ScheduledFireTimeUtc,
            context.FireTimeUtc);

        var sw = Stopwatch.StartNew();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var service = scope.ServiceProvider.GetRequiredService<IRecurringTransactionService>();

            var result = await service.GenerateAllDueAsync(context.CancellationToken);


            // All properties are structured log fields — queryable in Seq.
            // Filter on "FailedTemplates > 0" to find runs with partial failures.
            _logger.LogInformation(
                "Recurring transaction generation completed. " +
                "Templates: {TemplatesProcessed}, " +
                "Generated: {TransactionsGenerated}, " +
                "Failed: {FailedTemplates}, " +
                "Elapsed: {ElapsedMs}ms",
                result.TemplatesProcessed,
                result.TransactionsGenerated,
                result.FailedTemplates,
                sw.ElapsedMilliseconds);

            // Logging each per-template failure as its own entry to enable filtering by RecurringTransactionId or UserId in Seq to investigate specific failures.
            foreach (var failure in result.Failures)
            {
                _logger.LogError(
                    "Failed to process recurring template {RecurringTransactionId} " +
                    "for user {UserId}: {Error}",
                    failure.RecurringTransactionId,
                    failure.UserId,
                    failure.Error);
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down cleanly. Not an error — do not rethrow.
            sw.Stop();
            _logger.LogWarning(
                "Recurring transaction generation was cancelled after {ElapsedMs}ms " +
                "(graceful shutdown).",
                sw.ElapsedMilliseconds);
        }

        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Recurring transaction generation failed after {ElapsedMs}ms.",
                sw.ElapsedMilliseconds);
            
            // JobExecutionException tells Quartz this execution failed.
            // refireImmediately: false — do not retry immediately. Wait for the
            // next scheduled trigger. Immediately retrying would almost certainly hit the same error again.
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}