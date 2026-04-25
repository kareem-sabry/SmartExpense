using FluentValidation;
using SmartExpense.Application.Interfaces;
using SmartExpense.Application.Validators.Auth;
using SmartExpense.Core.Models;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Services;

namespace SmartExpense.Api.Extensions;

public static class AppServicesExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.JwtOptionsKey));
        services.Configure<AdminUserOptions>(
            configuration.GetSection("AdminUser"));
        services.Configure<EmailOptions>(
            configuration.GetSection("EmailOptions"));

        // FluentValidation — discovers all validators in the Application assembly
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(
            lifetime: ServiceLifetime.Singleton); // validators are stateless — Singleton is safe and efficient

        // Infrastructure
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IAuthTokenProcessor, AuthTokenProcessorService>();

        // Domain services
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}