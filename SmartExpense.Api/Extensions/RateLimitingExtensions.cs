using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SmartExpense.Application.Dtos.Auth;

namespace SmartExpense.Api.Extensions;


public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.Identity?.Name
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 100,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        }));

            options.AddFixedWindowLimiter("auth", o =>
            {
                o.PermitLimit = 5;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("api", o =>
            {
                o.PermitLimit = 50;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueLimit = 0;
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsJsonAsync(new BasicResponse
                {
                    Succeeded = false,
                    Message = "Too many requests. Please try again later."
                }, token);
            };
        });

        return services;
    }
}