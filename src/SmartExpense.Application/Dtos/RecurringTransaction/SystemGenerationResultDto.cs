namespace SmartExpense.Application.Dtos.RecurringTransaction;

/// <summary>
/// Result of a system-wide recurring transaction generation sweep.
/// Returned by GenerateAllDueAsync and consumed by the background job for
/// structured log output. Separate from GenerateTransactionsResultDto because:
/// - That DTO returns the full list of generated transactions (appropriate for
///   a per-user API response, not for logging thousands of records).
/// - This DTO includes failure details, which the existing DTO cannot represent.
/// </summary>
public class SystemGenerationResultDto
{
    /// <summary>Total active, in-range templates examined in this run.</summary>
    public int TemplatesProcessed { get; set; }

    /// <summary>Total new Transaction rows inserted across all templates.</summary>
    public int TransactionsGenerated { get; set; }

    /// <summary>
    /// Count of templates whose processing threw an exception.
    /// Other templates are unaffected — see GenerateAllDueAsync for isolation design.
    /// </summary>
    public int FailedTemplates { get; set; }

    /// <summary>
    /// Per-failure detail. Logged by the job as separate structured entries
    /// so you can query by RecurringTransactionId or UserId in Seq.
    /// </summary>
    public List<TemplateFailureInfo> Failures { get; set; } = new();
}

/// <summary>
/// Captures the context for one template that failed during the sweep.
/// </summary>
public class TemplateFailureInfo
{
    /// <summary>The Id of the RecurringTransaction template that failed.</summary>
    public int RecurringTransactionId { get; set; }

    /// <summary>The UserId who owns the failed template.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The exception message. The full stack trace is logged separately
    /// via LogError, which accepts the exception object.
    /// </summary>
    public string Error { get; set; } = string.Empty;
}