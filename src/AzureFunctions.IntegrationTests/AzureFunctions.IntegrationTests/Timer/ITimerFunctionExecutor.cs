namespace AzureFunctions.IntegrationTests.Timer;

/// <summary>
/// Abstraction for firing timer-triggered Azure Functions in-process during integration tests,
/// without a live timer scheduler or Azure Functions host.
/// </summary>
/// <remarks>
/// Obtain a concrete instance via
/// <c>FunctionAppFactory&lt;TEntryPoint&gt;.CreateTimerFunctionExecutor()</c>.
/// </remarks>
public interface ITimerFunctionExecutor
{
    /// <summary>
    /// Fires the timer-triggered function identified by <paramref name="functionName"/> as if
    /// it were triggered by the scheduler.
    /// </summary>
    /// <param name="functionName">The name declared in <c>[Function("…")]</c>.</param>
    /// <param name="isPastDue">
    /// When <see langword="true"/> the <see cref="Microsoft.Azure.Functions.Worker.TimerInfo"/>
    /// supplied to the function reports <c>IsPastDue = true</c>, allowing tests to verify
    /// past-due guard logic.
    /// </param>
    /// <returns>
    /// A <see cref="TimerFunctionExecutionResult"/> containing the <c>TimerInfo</c> that was
    /// passed to the function.
    /// </returns>
    Task<TimerFunctionExecutionResult> FireAsync(string functionName, bool isPastDue = false);
}