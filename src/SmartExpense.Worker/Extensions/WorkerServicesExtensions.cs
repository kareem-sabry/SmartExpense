using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Interceptors;
using SmartExpense.Infrastructure.Services;

namespace SmartExpense.Worker.Extensions;

public static class WorkerServicesExtensions
{
    /// <summary>
    /// Registers the minimum set of services the Worker needs to function.
    /// Intentionally excludes all web-only services: JWT, Swagger, CORS,
    /// rate limiting, FluentValidation, email, analytics, budget, admin.
    /// </summary>
    public static IServiceCollection AddWorkerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AuditInterceptor requires IHttpContextAccessor even in non-HTTP contexts.
        // HttpContext is always null in the Worker — the interceptor handles this
        // gracefully by falling back to ApplicationConstants.SystemUser for all
        // CreatedBy/UpdatedBy audit stamps.
        services.AddHttpContextAccessor();

        // Register AppDbContext with the same AuditInterceptor configuration as the API.
        // The Worker does NOT call MigrateAsync — the API owns migration execution.
        // The Worker connects to an already-migrated database.
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var dateTimeProvider = sp.GetRequiredService<IDateTimeProvider>();
            var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException(
                        "Connection string 'DefaultConnection' is not configured."),
                sql => sql.MigrationsAssembly("SmartExpense.Infrastructure"));

            options.AddInterceptors(
                new AuditInterceptor(httpContextAccessor, dateTimeProvider));
        });

        // Singleton: stateless, safe to share across all scopes and executions.
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // Scoped: one instance per DI scope. The job creates a new scope per execution
        // (via IServiceScopeFactory), so these are effectively per-execution instances.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();

        return services;
    }
}