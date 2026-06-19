using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SmartExpense.Application.Interfaces;
using SmartExpense.Core.Entities;
using SmartExpense.Core.Models;
using SmartExpense.Infrastructure.Data;

namespace SmartExpense.Api.Extensions;

public static class DatabaseSeedingExtensions
{
    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var services = scope.ServiceProvider;

        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DatabaseSeedingExtensions));

        try
        {
            logger.LogInformation("Starting database seeding...");

            var context = services.GetRequiredService<AppDbContext>();
            var userManager = services.GetRequiredService<UserManager<User>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var adminOptions = services.GetRequiredService<IOptions<AdminUserOptions>>();
            var dateTimeProvider = services.GetRequiredService<IDateTimeProvider>();

            await DbInitializer.SeedDataAsync(
                context,
                userManager,
                roleManager,
                adminOptions,
                logger,
                dateTimeProvider);

            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            // rethrow so Railway shows the real startup error instead of
            // starting a zombie process where every API call returns 500.
            logger.LogCritical(ex, "Database seeding failed. Stopping application.");
            throw;
        }
    }
}