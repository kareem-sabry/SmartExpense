using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartExpense.Core.Exceptions;

namespace SmartExpense.Api.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = GetStatusAndTitle(exception);

        // Expected domain exceptions (4xx) are informational — log as Warning.
        // Unexpected failures (5xx) are actual errors that need investigation.
        if (statusCode >= 500)
            _logger.LogError(exception,
                "Unhandled server error on {Method} {Path}: {Message}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception.Message);
        else
            _logger.LogWarning(
                "{ExceptionType} on {Method} {Path}: {Message}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception.Message);

        var problemDetails = CreateProblemDetails(httpContext, exception, statusCode, title);

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private (int statusCode, string title) GetStatusAndTitle(Exception exception)
    {
        return exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation Error"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "Invalid Operation"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Argument"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };
    }

    private ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception, int statusCode,
        string title)
    {
        var problemDetails = exception is ValidationException validationEx
            ? new ValidationProblemDetails(validationEx.Errors)
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message
            }
            : new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = GetDetailMessage(exception, statusCode)
            };
        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Type = $"https://httpstatuses.com/{statusCode}";
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        //stack trace is only included in development
        if (_environment.IsDevelopment() && statusCode == 500)
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;

        return problemDetails;
    }

    private string GetDetailMessage(Exception exception, int statusCode)
    {
        // internal details are not exposed in production for 500 errors
        if (statusCode == 500 && !_environment.IsDevelopment())
            return "An unexpected error occurred. Please try again later.";

        return exception.Message;
    }
}