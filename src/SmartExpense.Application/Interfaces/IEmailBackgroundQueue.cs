namespace SmartExpense.Application.Interfaces;

/// <summary>
/// In-memory queue for fire-and-forget email delivery.
/// Request handlers enqueue instantly and return. A BackgroundService drains and delivers.
/// </summary>
public interface IEmailBackgroundQueue
{
    /// <summary>
    /// Adds an email to the queue and returns immediately.
    /// Never blocks the caller — designed to be called from request handlers.
    /// </summary>
    void Enqueue(string toEmail, string subject, string body);

    /// <summary>
    /// Waits for the next queued email and returns it.
    /// Called only by the background consumer — blocks until an item is available.
    /// </summary>
    ValueTask<(string ToEmail, string Subject, string Body)> DequeueAsync(CancellationToken cancellationToken);
}