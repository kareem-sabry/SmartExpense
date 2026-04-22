using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Interceptors;

namespace SmartExpense.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
            var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.MigrationsAssembly("SmartExpense.Infrastructure")
            );
            options.AddInterceptors(new AuditInterceptor(httpContextAccessor, dateTimeProvider));
        });

        return services;
    }
}
