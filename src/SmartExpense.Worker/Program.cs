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

    // Initialize the Quartz SQL Server schema before the Quartz scheduler starts.
    //
    // Timing: host is built (DI container ready, logger resolvable) but not yet
    // running (Quartz scheduler not started). This is the correct window.
    //
    // In production CI/CD: this runs as a pre-deployment step, the same way
    // EF Core migrations do. Here it is self-bootstrapping for local Docker.
    var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' is required.");

    await QuartzSchemaInitializer.EnsureSchemaCreatedAsync(
        connectionString,
        startupLogger);

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
