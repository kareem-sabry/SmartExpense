using Microsoft.EntityFrameworkCore;
using SmartExpense.Application.Interfaces;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Interceptors;

namespace SmartExpense.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DATABASE_URL"];

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new Exception("No database connection string found");

            // Railway uses postgres:// format → convert it
            if (connectionString.StartsWith("postgres://"))
            {
                var uri = new Uri(connectionString);

                var userInfo = uri.UserInfo.Split(':');

                connectionString =
                    $"Host={uri.Host};" +
                    $"Port={uri.Port};" +
                    $"Database={uri.AbsolutePath.Trim('/')};" +
                    $"Username={userInfo[0]};" +
                    $"Password={userInfo[1]};" +
                    "SSL Mode=Require;Trust Server Certificate=true";
            }

            options.UseNpgsql(connectionString);
        });
        return services;
    }

    private static string ConvertPostgresUriToNpgsql(string uri)
    {
        var u = new Uri(uri);
        var userInfo = u.UserInfo.Split(':',2);
        return $"Host={u.Host};Port={u.Port};Database={u.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
    }
}
