using System.Threading.Channels;
using SmartExpense.Application.Interfaces;

namespace SmartExpense.Infrastructure.Services;

/// <summary>
/// Unbounded in-memory Channel queue for email jobs.
/// Registered as Singleton — one queue for the lifetime of the process.
/// SingleReader = true: only the background service reads from it, which lets the
/// channel skip unnecessary locking on the consumer side.
/// </summary>
public sealed class EmailBackgroundQueue : IEmailBackgroundQueue
{
    private readonly Channel<(string ToEmail, string Subject, string Body)> _channel =
        Channel.CreateUnbounded<(string, string, string)>(
            new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(string toEmail, string subject, string body)
    {
        // TryWrite on an unbounded channel never returns false — safe to ignore return value
        _channel.Writer.TryWrite((toEmail, subject, body));
    }

    public ValueTask<(string ToEmail, string Subject, string Body)> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}