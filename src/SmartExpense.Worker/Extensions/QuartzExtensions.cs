using Quartz;
using SmartExpense.Worker.Jobs;

namespace SmartExpense.Worker.Extensions;

public static class QuartzExtensions
{
    // Quartz cron format (6 fields): Seconds Minutes Hours DayOfMonth Month DayOfWeek
    // "0 5 0 * * ?" = fire at 00:05:00 UTC every day.
    // "?" in DayOfWeek = "don't care" — required when DayOfMonth already specifies "every day."
    private const string DefaultCron = "0 5 0 * * ?";

    public static IServiceCollection AddQuartzScheduling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddQuartz(q =>
        {
            q.SchedulerId = "SmartExpense-Worker";
            q.SchedulerName = "SmartExpense Recurring Transaction Scheduler";

            // AdoJobStore: persists all scheduler state to SQL Server QRTZ_* tables.
            q.UsePersistentStore(store =>
            {
                // UseProperties: use string keys for Quartz internal data maps.
                // Required for correct AdoJobStore operation.
                store.UseProperties = true;

                // If the DB connection is temporarily lost, Quartz retries every 15s
                // rather than crashing the process.
                store.RetryInterval = TimeSpan.FromSeconds(15);

                store.UseSqlServer(sql =>
                {
                    sql.ConnectionString =
                        configuration.GetConnectionString("DefaultConnection")
                        ?? throw new InvalidOperationException(
                            "Connection string 'DefaultConnection' is required for Quartz AdoJobStore.");

                    // TablePrefix must match what the schema script created.
                    sql.TablePrefix = "QRTZ_";
                });

                // JSON serialization for job data maps.
                // Requires the Quartz.Serialization.Json package.
                // More portable than the deprecated BinaryFormatter and human-readable
                // when you inspect QRTZ_JOB_DETAILS rows directly in SSMS.
                store.UseJsonSerializer();
            });

            var jobKey = new JobKey("RecurringTransactionGenerationJob", "SmartExpense");

            q.AddJob<RecurringTransactionGenerationJob>(opts => opts
                .WithIdentity(jobKey)
                .WithDescription(
                    "Daily sweep: generates pending transactions for all active " +
                    "recurring templates across all users.")
                // StoreDurably: the QRTZ_JOB_DETAILS row survives even if the trigger
                // is temporarily removed. The job definition is never lost.
                .StoreDurably());

            // Read cron from config so it can be overridden per environment without
            // recompiling. Falls back to the hardcoded default if not set.
            var cron = configuration["Quartz:RecurringGenerationCron"] ?? DefaultCron;

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("RecurringTransactionGenerationTrigger", "SmartExpense")
                // InTimeZone(Utc): the cron fires in UTC, not the host's local time zone.
                // Docker containers usually run in UTC, but being explicit prevents
                // surprises if the host is ever configured differently.
                .WithCronSchedule(cron, x => x.InTimeZone(TimeZoneInfo.Utc))
                .WithDescription("Fires daily at 00:05:00 UTC."));
        });

        services.AddQuartzHostedService(options =>
        {
            // Block host shutdown until any running job finishes.
            // Prevents a generation sweep from being killed mid-batch when Docker
            // sends SIGTERM (on deploy, scale-down, or Ctrl+C).
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}