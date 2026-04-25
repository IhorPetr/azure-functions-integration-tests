using System.Text.Json;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AzureFunctions.IntegrationTests.Mocks.Durable;

/// <summary>
/// Concrete, configurable implementation of <see cref="TaskOrchestrationContext"/> for
/// in-process integration tests.  Use the <c>MockActivity</c> overloads to define what each
/// activity call should return, then pass an instance of this class to
/// <c>IDurableFunctionExecutor.InvokeOrchestratorAsync</c>.
/// </summary>
public class MockTaskOrchestrationContext : TaskOrchestrationContext
{
    private readonly object? _input;
    private readonly Dictionary<string, Func<object?, Task<object?>>> _activityHandlers =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Initialises a new <see cref="MockTaskOrchestrationContext"/>.
    /// </summary>
    /// <param name="input">
    /// The input that will be returned by <see cref="GetInput{T}()"/>. May be the exact
    /// typed value or an object that will be round-tripped through JSON for type conversion.
    /// </param>
    /// <param name="orchestratorName">Optional logical name exposed via <see cref="Name"/>.</param>
    /// <param name="instanceId">Optional instance ID; a new GUID is used when omitted.</param>
    public MockTaskOrchestrationContext(
        object? input = null,
        string orchestratorName = "mock-orchestrator",
        string? instanceId = null)
    {
        _input = input;
        Name = orchestratorName;
        InstanceId = instanceId ?? Guid.NewGuid().ToString();
    }

    // ── Captured state (inspectable from tests) ──────────────────────────────

    /// <summary>Gets the custom status set by the orchestrator via <see cref="SetCustomStatus"/>.</summary>
    public object? CustomStatus { get; private set; }

    // ── MockActivity overloads ────────────────────────────────────────────────

    /// <summary>
    /// Configures a mock for an activity that performs a side-effect but does not return
    /// a meaningful value (void / fire-and-forget activities).
    /// </summary>
    /// <typeparam name="TInput">The activity's input type.</typeparam>
    /// <param name="activityName">The activity function name (case-insensitive).</param>
    /// <param name="action">
    /// An action that receives the activity input. Return value is discarded.
    /// </param>
    /// <returns>This instance to allow fluent chaining.</returns>
    public MockTaskOrchestrationContext MockActivity<TInput>(string activityName, Action<TInput?> action)
    {
        _activityHandlers[activityName] = input =>
        {
            var typed = ConvertValue<TInput>(input);
            action(typed);
            return Task.FromResult<object?>(null);
        };
        return this;
    }

    /// <summary>
    /// Configures a mock for an activity that always returns the same fixed value.
    /// </summary>
    /// <typeparam name="TResult">The activity's return type.</typeparam>
    /// <param name="activityName">The activity function name (case-insensitive).</param>
    /// <param name="result">The value to return whenever the orchestrator calls this activity.</param>
    /// <returns>This instance to allow fluent chaining.</returns>
    public MockTaskOrchestrationContext MockActivity<TResult>(string activityName, TResult result)
    {
        _activityHandlers[activityName] = _ => Task.FromResult<object?>(result);
        return this;
    }

    /// <summary>
    /// Configures a mock for an activity whose return value depends on the input.
    /// </summary>
    /// <typeparam name="TInput">The activity's input type.</typeparam>
    /// <typeparam name="TResult">The activity's return type.</typeparam>
    /// <param name="activityName">The activity function name (case-insensitive).</param>
    /// <param name="handler">
    /// A synchronous function that receives the input and returns the result.
    /// </param>
    /// <returns>This instance to allow fluent chaining.</returns>
    public MockTaskOrchestrationContext MockActivity<TInput, TResult>(
        string activityName,
        Func<TInput?, TResult> handler)
    {
        _activityHandlers[activityName] = input =>
        {
            var typed = ConvertValue<TInput>(input);
            return Task.FromResult<object?>(handler(typed));
        };
        return this;
    }

    /// <summary>
    /// Configures a mock for an activity using an async handler whose return value
    /// depends on the input.
    /// </summary>
    /// <typeparam name="TInput">The activity's input type.</typeparam>
    /// <typeparam name="TResult">The activity's return type.</typeparam>
    /// <param name="activityName">The activity function name (case-insensitive).</param>
    /// <param name="handler">An async function that receives the input and returns the result.</param>
    /// <returns>This instance to allow fluent chaining.</returns>
    public MockTaskOrchestrationContext MockActivity<TInput, TResult>(
        string activityName,
        Func<TInput?, Task<TResult>> handler)
    {
        _activityHandlers[activityName] = async input =>
        {
            var typed = ConvertValue<TInput>(input);
            return (object?)await handler(typed);
        };
        return this;
    }

    // ── Abstract overrides ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override TaskName Name { get; }

    /// <inheritdoc/>
    public override string InstanceId { get; }

    /// <inheritdoc/>
    public override ParentOrchestrationInstance? Parent => null;

    /// <inheritdoc/>
    public override DateTime CurrentUtcDateTime => DateTime.UtcNow;

    /// <inheritdoc/>
    public override bool IsReplaying => false;

    /// <inheritdoc/>
    protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

    /// <inheritdoc/>
    public override T? GetInput<T>() where T : default => ConvertValue<T>(_input);

    /// <inheritdoc/>
    public override async Task<TResult> CallActivityAsync<TResult>(
        TaskName name, object? input = null, TaskOptions? options = null)
    {
        if (!_activityHandlers.TryGetValue(name.Name, out var handler))
        {
            throw new InvalidOperationException(
                $"No mock configured for activity '{name.Name}'. " +
                $"Call MockActivity(\"{name.Name}\", ...) on the {nameof(MockTaskOrchestrationContext)} " +
                $"before invoking the orchestrator.");
        }

        var raw = await handler(input);
        return ConvertValue<TResult>(raw)!;
    }

    /// <inheritdoc/>
    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public override Task<T> WaitForExternalEvent<T>(
        string eventName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            $"'{nameof(WaitForExternalEvent)}' is not supported in integration tests. " +
            "Configure mock activity handlers instead.");

    /// <inheritdoc/>
    public override void SendEvent(string instanceId, string eventName, object payload) { }

    /// <inheritdoc/>
    public override void SetCustomStatus(object? customStatus) => CustomStatus = customStatus;

    /// <inheritdoc/>
    public override Task<TResult> CallSubOrchestratorAsync<TResult>(
        TaskName orchestratorName, object? input = null, TaskOptions? options = null)
        => throw new NotSupportedException(
            $"'{nameof(CallSubOrchestratorAsync)}' is not supported in integration tests.");

    /// <inheritdoc/>
    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) { }

    /// <inheritdoc/>
    public override Guid NewGuid() => Guid.NewGuid();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T? ConvertValue<T>(object? value)
    {
        if (value is null) return default;
        if (value is T typed) return typed;

        // Fall back to JSON round-trip for structural conversions
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }
}