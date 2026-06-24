using System.Reflection;
using Microsoft.Data.SqlClient;

namespace SmartExpense.Worker.Infrastructure;

/// <summary>
/// Ensures the Quartz.NET QRTZ_* tables exist in SQL Server before the
/// scheduler starts. Uses the official schema script embedded in the assembly.
///
/// Checks for QRTZ_JOB_DETAILS as a proxy for "the full schema exists" — it
/// is always the first table created and is the most fundamental in the schema.
///
/// In production CI/CD this runs as a pre-deployment step (same pattern as
/// EF Core migrations). Here it runs at startup so Docker is self-bootstrapping.
/// </summary>
internal static class QuartzSchemaInitializer
{
    private const string CheckSql = """
                                    SELECT COUNT(*)
                                    FROM   INFORMATION_SCHEMA.TABLES
                                    WHERE  TABLE_SCHEMA = 'dbo'
                                    AND    TABLE_NAME   = 'QRTZ_JOB_DETAILS'
                                    """;

    public static async Task EnsureSchemaCreatedAsync(
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check whether the Quartz schema already exists.
        await using var checkCmd = new SqlCommand(CheckSql, connection);
        var count = (int)(await checkCmd.ExecuteScalarAsync(cancellationToken))!;

        if (count > 0)
        {
            logger.LogDebug("Quartz schema already exists — skipping initialization");
            return;
        }

        logger.LogInformation("Quartz schema not found — initializing QRTZ_* tables");

        var script = LoadEmbeddedScript("quartz-sqlserver.sql");

        // The Quartz SQL Server script uses GO to separate T-SQL batches.
        // GO is a SQL Server Management Studio convention — it is NOT actual T-SQL.
        // SqlCommand does not understand GO and throws "Incorrect syntax near 'GO'"
        // if you execute the full script at once. Split and execute each batch.
        var batches = script.Split(
            ["\r\nGO", "\nGO"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;
            
            if (batch.TrimStart().StartsWith("USE ", StringComparison.OrdinalIgnoreCase))
                continue;
            await using var cmd = new SqlCommand(batch, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        logger.LogInformation("Quartz schema initialized successfully");
    }

    private static string LoadEmbeddedScript(string fileName)
    {
        var assembly = typeof(QuartzSchemaInitializer).Assembly;

        // GetManifestResourceNames returns names like:
        // "SmartExpense.Worker.Infrastructure.quartz-sqlserver.sql"
        // Match by suffix to avoid hardcoding the full resource name.
        var resourceName = assembly
                               .GetManifestResourceNames()
                               .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{fileName}' not found in {assembly.GetName().Name}. " +
                               "Verify the file exists and is marked <EmbeddedResource> in the .csproj.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException(
                               $"Could not open stream for embedded resource '{resourceName}'.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}