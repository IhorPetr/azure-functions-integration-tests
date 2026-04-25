using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.SampleApp;

/// <summary>
/// Sample timer-triggered functions for use in integration tests via
/// <c>FunctionAppFactory.CreateTimerFunctionExecutor()</c>.
/// </summary>
public class CleanupTimerFunctions
{
    // ── Shared in-memory state (inspectable from tests) ───────────────────────

    /// <summary>
    /// Number of times <c>DailyCleanup</c> was fired during the current test run.
    /// </summary>
    public static int CleanupRunCount { get; private set; }

    /// <summary>
    /// Whether the last <c>DailyCleanup</c> invocation received a past-due timer.
    /// </summary>
    public static bool LastRunWasPastDue { get; private set; }

    /// <summary>Resets all counters between tests.</summary>
    public static void ResetState()
    {
        CleanupRunCount = 0;
        LastRunWasPastDue = false;
    }

    // ── Timer function ────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a daily cleanup task at midnight UTC.
    /// Records <see cref="TimerInfo.IsPastDue"/> so tests can verify past-due guard logic.
    /// </summary>
    [Function("DailyCleanup")]
    public Task DailyCleanup(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timer,
        FunctionContext context)
    {
        LastRunWasPastDue = timer.IsPastDue;
        CleanupRunCount++;
        return Task.CompletedTask;
    }
}

