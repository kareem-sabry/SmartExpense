using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartExpense.Application.Interfaces;

namespace SmartExpense.Infrastructure.Services;

/// <summary>
/// Long-running hosted service that drains the email queue and delivers messages.
/// Runs on a background thread — completely decoupled from the HTTP request pipeline.
/// If SMTP fails, the error is logged and the job is dropped (acceptable for a portfolio project).
/// For production, add Polly retry + dead-letter queue here.
/// </summary>
public sealed class EmailSenderBackgroundService : BackgroundService
{
    private readonly IEmailBackgroundQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSenderBackgroundService> _logger;

    public EmailSenderBackgroundService(
        IEmailBackgroundQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailSenderBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email background sender started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (toEmail, subject, body) = await _queue.DequeueAsync(stoppingToken);

                try
                {
                    // IEmailService is Scoped — must create a scope per job
                    // (BackgroundService itself is Singleton, can't inject Scoped directly)
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    await emailService.SendEmailAsync(toEmail, subject, body);

                    _logger.LogInformation("Email delivered to {ToEmail}", toEmail);
                }
                catch (Exception ex)
                {
                    // Don't crash the loop — log and move on to the next job
                    _logger.LogError(ex, "Failed to deliver email to {ToEmail} — job dropped", toEmail);
                }
            }
            catch (OperationCanceledException)
            {
                // App shutting down — exit cleanly
                break;
            }
        }

        _logger.LogInformation("Email background sender stopped");
    }
}