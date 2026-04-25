using AzureFunctions.IntegrationTests.Timer;

namespace AzureFunctions.IntegrationTests.SampleApp.Tests;

/// <summary>
/// Integration tests for <see cref="TimerFunctionExecutor"/>.
/// Covers on-schedule firing, past-due firing, and error handling — all executed in-process
/// without a live timer scheduler or Azure Functions host.
/// </summary>
public class TimerFunctionIntegrationTests : IClassFixture<FunctionAppFactory<Program>>, IDisposable
{
    private readonly FunctionAppFactory<Program> _factory;

    public TimerFunctionIntegrationTests(FunctionAppFactory<Program> factory)
    {
        _factory = factory;
        CleanupTimerFunctions.ResetState();
    }

    public void Dispose() => CleanupTimerFunctions.ResetState();

    // ── Normal fire (not past-due) ────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>FireAsync</c> invokes the timer function, increments the run counter,
    /// and reports <c>IsPastDue = false</c> when fired on schedule.
    /// </summary>
    [Fact]
    public async Task FireAsync_OnSchedule_FunctionExecutesAndIsPastDueFalse()
    {
        var executor = _factory.CreateTimerFunctionExecutor();

        var result = await executor.FireAsync("DailyCleanup");

        Assert.Equal(1, CleanupTimerFunctions.CleanupRunCount);
        Assert.False(CleanupTimerFunctions.LastRunWasPastDue);
        Assert.False(result.TimerInfo.IsPastDue);
    }

    /// <summary>
    /// Verifies that calling <c>FireAsync</c> multiple times increments the counter on each call.
    /// </summary>
    [Fact]
    public async Task FireAsync_MultipleFires_CounterIncrements()
    {
        var executor = _factory.CreateTimerFunctionExecutor();

        await executor.FireAsync("DailyCleanup");
        await executor.FireAsync("DailyCleanup");
        await executor.FireAsync("DailyCleanup");

        Assert.Equal(3, CleanupTimerFunctions.CleanupRunCount);
    }

    // ── Past-due fire ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that setting <c>isPastDue = true</c> passes a <c>TimerInfo</c> with
    /// <c>IsPastDue = true</c> to the function, allowing tests to verify past-due guard logic.
    /// </summary>
    [Fact]
    public async Task FireAsync_PastDue_FunctionReceivesPastDueTrue()
    {
        var executor = _factory.CreateTimerFunctionExecutor();

        var result = await executor.FireAsync("DailyCleanup", isPastDue: true);

        Assert.True(CleanupTimerFunctions.LastRunWasPastDue);
        Assert.True(result.TimerInfo.IsPastDue);
        Assert.Equal(1, CleanupTimerFunctions.CleanupRunCount);
    }

    // ── TimerFunctionExecutionResult ──────────────────────────────────────────

    /// <summary>
    /// Verifies that the returned <see cref="TimerFunctionExecutionResult"/> contains a
    /// non-null <c>TimerInfo</c> with a populated <c>ScheduleStatus</c>.
    /// </summary>
    [Fact]
    public async Task FireAsync_Result_ContainsScheduleStatus()
    {
        var executor = _factory.CreateTimerFunctionExecutor();

        var result = await executor.FireAsync("DailyCleanup");

        Assert.NotNull(result.TimerInfo);
        Assert.NotNull(result.TimerInfo.ScheduleStatus);
        Assert.True(result.TimerInfo.ScheduleStatus.Next > result.TimerInfo.ScheduleStatus.Last);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that firing an unknown function name throws
    /// <see cref="InvalidOperationException"/> listing the available timer functions.
    /// </summary>
    [Fact]
    public async Task FireAsync_UnknownFunction_ThrowsInvalidOperationException()
    {
        var executor = _factory.CreateTimerFunctionExecutor();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.FireAsync("NonExistentTimerFunction"));
    }
}

