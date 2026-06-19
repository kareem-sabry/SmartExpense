using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Interceptors;

namespace SmartExpense.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DATABASE_URL"];

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "No database connection string found. " +
                    "Set ConnectionStrings__DefaultConnection or DATABASE_URL in Railway variables.");

            
            if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
            {
                connectionString = ConvertPostgresUriToNpgsql(connectionString);
            }

            var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
            var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

            options.UseNpgsql(connectionString, npgsql =>
            {
                //tells EF where migrations live (different project from startup)
                npgsql.MigrationsAssembly("SmartExpense.Infrastructure");

                
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });

            
            options.AddInterceptors(new AuditInterceptor(httpContextAccessor, dateTimeProvider));
        });

        return services;
    }

    private static string ConvertPostgresUriToNpgsql(string uri)
    {
        var u = new Uri(uri);
        var userInfo = u.UserInfo.Split(':', 2);
        return
            $"Host={u.Host};" +
            $"Port={u.Port};" +
            $"Database={u.AbsolutePath.TrimStart('/')};" +
            $"Username={userInfo[0]};" +
            $"Password={userInfo[1]};" +
            "SSL Mode=Require;Trust Server Certificate=true";
    }
}