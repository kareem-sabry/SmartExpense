using FluentValidation;
using SmartExpense.Application.Interfaces;
using SmartExpense.Application.Validators.Auth;
using SmartExpense.Core.Models;
using SmartExpense.Infrastructure.Data;
using SmartExpense.Infrastructure.Services;
using EmailSenderBackgroundService = SmartExpense.Infrastructure.Services.EmailSenderBackgroundService;
namespace SmartExpense.Api.Extensions;

public static class AppServicesExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.JwtOptionsKey));
        services.Configure<AdminUserOptions>(configuration.GetSection("AdminUser"));
        services.Configure<EmailOptions>(configuration.GetSection("EmailOptions"));

        // FluentValidation — discovers all validators in the Application assembly
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(
            lifetime: ServiceLifetime.Singleton);

        // Infrastructure
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IAuthTokenProcessor, AuthTokenProcessorService>();

        // Email — service handles actual SMTP delivery, queue decouples it from requests
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IEmailBackgroundQueue, EmailBackgroundQueue>();
        services.AddHostedService<EmailSenderBackgroundService>();

        // Domain services
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}