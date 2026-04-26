using AzureFunctions.IntegrationTests.Mocks.Durable;

namespace AzureFunctions.IntegrationTests.Durable;

/// <summary>
/// Abstraction for executing Durable Functions orchestrators and activities
/// in-process during integration tests, without a live Durable Task hub.
/// </summary>
/// <remarks>
/// Obtain a concrete instance via
/// <c>FunctionAppFactory&lt;TEntryPoint&gt;.CreateDurableFunctionExecutor()</c>.
/// </remarks>
public interface IDurableFunctionExecutor
{
    // ── Activity execution ────────────────────────────────────────────────────

    /// <summary>
    /// Executes the activity function identified by <paramref name="functionName"/> with a
    /// typed input and returns its strongly-typed output.
    /// </summary>
    /// <typeparam name="TInput">The activity's input type.</typeparam>
    /// <typeparam name="TResult">The expected return type of the activity.</typeparam>
    /// <param name="functionName">The name declared in <c>[Function("…")]</c>.</param>
    /// <param name="input">The input payload to pass to the activity.</param>
    /// <param name="instanceId">
    /// Optional orchestration instance ID exposed via <c>TaskActivityContext.InstanceId</c>.
    /// A new GUID is used when omitted.
    /// </param>
    /// <returns>The deserialized return value of the activity function.</returns>
    Task<TResult?> ExecuteActivityAsync<TInput, TResult>(
        string functionName, TInput input, string? instanceId = null);

    /// <summary>
    /// Executes the activity function identified by <paramref name="functionName"/> with no
    /// meaningful return value (fire-and-forget variant).
    /// </summary>
    /// <typeparam name="TInput">The activity's input type.</typeparam>
    /// <param name="functionName">The name declared in <c>[Function("…")]</c>.</param>
    /// <param name="input">The input payload to pass to the activity.</param>
    /// <param name="instanceId">
    /// Optional orchestration instance ID. A new GUID is used when omitted.
    /// </param>
    Task ExecuteActivityAsync<TInput>(
        string functionName, TInput input, string? instanceId = null);

    /// <summary>
    /// Executes the activity function identified by <paramref name="functionName"/> with an
    /// untyped input and returns its output deserialized as <typeparamref name="TResult"/>.
    /// The input is JSON-round-tripped to the activity's declared parameter type when the
    /// supplied object type does not match exactly.
    /// </summary>
    /// <typeparam name="TResult">The expected return type of the activity.</typeparam>
    /// <param name="functionName">The name declared in <c>[Function("…")]</c>.</param>
    /// <param name="input">
    /// The untyped input payload (e.g. an anonymous object). May be <see langword="null"/>
    /// for activities that accept a nullable input.
    /// </param>
    /// <param name="instanceId">
    /// Optional orchestration instance ID. A new GUID is used when omitted.
    /// </param>
    Task<TResult?> ExecuteActivityAsync<TResult>(
        string functionName, object? input = null, string? instanceId = null);

    // ── Orchestrator execution ────────────────────────────────────────────────

    /// <summary>
    /// Executes the orchestrator function identified by <paramref name="functionName"/> and
    /// returns its result deserialized as <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The expected return type of the orchestrator.</typeparam>
    /// <param name="functionName">The name declared in <c>[Function("…")]</c>.</param>
    /// <param name="context">
    /// A <see cref="MockTaskOrchestrationContext"/> pre-configured with the orchestrator input
    /// and any activity mocks the orchestrator will call. Use <c>MockActivity</c> fluent methods
    /// to define activity responses before invoking.
    /// </param>
    /// <returns>The deserialized return value of the orchestrator function.</returns>
    Task<TResult?> ExecuteOrchestratorAsync<TResult>(
        string functionName, MockTaskOrchestrationContext context);

    /// <summary>
    /// Executes the orchestrator function identified by <paramref name="functionName"/> when
    /// no return value is expected (fire-and-forget orchestrator).
    /// </summary>
    /// <param name="functionName">The name declared in <c>[Function("…")]</c>.</param>
    /// <param name="context">
    /// A <see cref="MockTaskOrchestrationContext"/> pre-configured with the orchestrator input
    /// and any activity mocks the orchestrator will call.
    /// </param>
    Task ExecuteOrchestratorAsync(
        string functionName, MockTaskOrchestrationContext context);
}