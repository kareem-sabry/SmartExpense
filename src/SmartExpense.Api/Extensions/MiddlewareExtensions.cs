using System.Security.Claims;
using Serilog;
using Serilog.Events;

namespace SmartExpense.Api.Extensions;

public static class MiddlewareExtensions
{
    public static void UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:;";

            await next();
        });
    }

    public static void UseStructuredRequestLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0.0000} ms";

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent",
                    httpContext.Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown");
                diagnosticContext.Set("UserId",
                    httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
                diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
            };

            options.GetLevel = (httpContext, _, ex) =>
                httpContext.Request.Path.StartsWithSegments("/health")
                    ? LogEventLevel.Verbose
                    : ex != null || httpContext.Response.StatusCode >= 500
                        ? LogEventLevel.Error
                        : LogEventLevel.Information;
        });
    }
}