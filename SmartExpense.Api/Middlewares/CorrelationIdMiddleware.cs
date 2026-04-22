using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace SmartExpense.Api.Middlewares;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        
        // Stamp the ASP.NET trace identifier so it shows in ProblemDetails
        context.TraceIdentifier = correlationId;
        
        // Echo it back on every response so the caller can correlate client-side
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var incoming)
            && !StringValues.IsNullOrEmpty(incoming))
        {
            // Honour the caller's ID — critical for tracing across services
            return incoming.ToString();
        }
        // Compact 16-char hex: readable in logs, doesn't blow up header size
        return Guid.NewGuid().ToString("N")[..16];
    }
}