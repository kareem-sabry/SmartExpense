using Microsoft.AspNetCore.Identity;
using SmartExpense.Core.Constants;
using SmartExpense.Core.Entities;
using SmartExpense.Infrastructure.Data;

namespace SmartExpense.Api.Extensions;

public static class IdentityExtensions
{
    public static IServiceCollection AddIdentityConfiguration(this IServiceCollection services)
    {
        services.AddIdentity<User, IdentityRole<Guid>>(opt =>
            {
                opt.Password.RequireDigit = true;
                opt.Password.RequireLowercase = true;
                opt.Password.RequireUppercase = true;
                opt.Password.RequireNonAlphanumeric = true;
                opt.Password.RequiredLength = ApplicationConstants.MinPasswordLength;

                opt.User.RequireUniqueEmail = true;

                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                opt.Lockout.MaxFailedAccessAttempts = 5;
                opt.Lockout.AllowedForNewUsers = true;

                opt.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultProvider;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(opt =>
        {
            opt.TokenLifespan =
                TimeSpan.FromHours(ApplicationConstants.PasswordResetTokenExpirationHours);
        });

        return services;
    }
}