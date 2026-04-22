using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using SmartExpense.Api.Extensions;
using SmartExpense.Api.Middlewares;
using SmartExpense.Infrastructure.Data;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services));

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services
        .AddDatabase(builder.Configuration)
        .AddIdentityConfiguration()
        .AddJwtAuthentication(builder.Configuration)
        .AddRateLimiting()
        .AddCorsConfiguration()
        .AddSwaggerConfiguration()
        .AddApplicationServices(builder.Configuration)
        .AddHealthChecks()
        .AddDbContextCheck<AppDbContext>();

    var app = builder.Build();

    await app.SeedDatabaseAsync();

    // =========================
    // OBSERVABILITY (OUTERMOST)
    // =========================
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseStructuredRequestLogging();

    // =========================
    // ERROR HANDLING
    // =========================
    app.UseExceptionHandler();

    // =========================
    // ENVIRONMENT CONFIG
    // =========================
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartExpense API v1");
            options.RoutePrefix = string.Empty;
        });

        app.UseCors("DevelopmentPolicy");
    }
    else
    {
        app.UseCors("ProductionPolicy");
        app.UseHsts();
    }

    // =========================
    // SECURITY & CORE PIPELINE
    // =========================
    app.UseSecurityHeaders();
    app.UseHttpsRedirection();

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Health checks (excluded from rate limiting)
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var result = new
            {
                status = report.Status.ToString(),
                checkedAt = DateTime.UtcNow,
                duration = report.TotalDuration,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration,
                    error = e.Value.Exception?.Message
                })
            };

            await context.Response.WriteAsJsonAsync(result);
        }
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}