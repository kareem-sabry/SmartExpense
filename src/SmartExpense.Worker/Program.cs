using Serilog;
using Serilog.Events;
using SmartExpense.Worker.Extensions;
using SmartExpense.Worker.Infrastructure;

// Bootstrap logger: handles any exception that occurs before the DI container
// is built. Mirrors the pattern in SmartExpense.Api/Program.cs.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Quartz", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Replace the default Microsoft logging provider with Serilog.
    // Reads sink, level, and enricher configuration from appsettings.json.
    builder.Services.AddSerilog((services, lc) =>
        lc.ReadFrom.Configuration(builder.Configuration)
          .ReadFrom.Services(services));

    builder.Services
        .AddWorkerInfrastructure(builder.Configuration)
        .AddQuartzScheduling(builder.Configuration);

    var host = builder.Build();

    var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException(
                               "Connection string 'DefaultConnection' is required.");

    const int maxAttempts = 12;
    const int retryDelaySeconds = 5;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await QuartzSchemaInitializer.EnsureSchemaCreatedAsync(
                connectionString, startupLogger);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            startupLogger.LogWarning(
                "Schema init failed (attempt {Attempt}/{Max}): {Message}. " +
                "Retrying in {Delay}s — waiting for API migrations to complete...",
                attempt, maxAttempts, ex.Message, retryDelaySeconds);

            await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
        }
    }

    host.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
    Environment.Exit(1);
}
finally
{
    // Flush remaining log entries before the process exits.
    Log.CloseAndFlush();
}
