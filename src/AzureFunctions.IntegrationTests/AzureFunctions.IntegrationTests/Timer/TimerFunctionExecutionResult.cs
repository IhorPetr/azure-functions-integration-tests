using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.Timer;

/// <summary>
/// The result returned by <see cref="ITimerFunctionExecutor"/> after invoking a timer-triggered function.
/// </summary>
public class TimerFunctionExecutionResult
{
    /// <summary>The <see cref="TimerInfo"/> that was supplied to the function.</summary>
    public TimerInfo TimerInfo { get; init; } = null!;
}