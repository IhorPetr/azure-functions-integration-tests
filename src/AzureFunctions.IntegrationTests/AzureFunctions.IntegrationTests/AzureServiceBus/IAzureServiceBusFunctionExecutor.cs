using Azure.Messaging.ServiceBus;

namespace AzureFunctions.IntegrationTests.AzureServiceBus;

/// <summary>
/// Abstraction for executing Azure Service Bus triggered Azure Functions
/// in-process during integration tests, without requiring a live Azure Service Bus namespace.
/// </summary>
/// <remarks>
/// Obtain a concrete instance via
/// <c>FunctionAppFactory&lt;TEntryPoint&gt;.CreateAzureServiceBusFunctionExecutor()</c>.
/// The interface can be used in test helpers or custom factory wrappers so that the
/// executor can be substituted with a test double if needed.
/// <para>
/// Each execute method is generic — specify the function's output-binding type as the
/// type argument to get a strongly-typed <c>ReturnValue</c> without casting.
/// Use <c>object</c> when the function returns <see langword="void"/> /
/// <see cref="System.Threading.Tasks.Task"/> or when the return value is not relevant to the test.
/// </para>
/// </remarks>
public interface IAzureServiceBusFunctionExecutor
{
    // ── Queue execution ───────────────────────────────────────────────────────

    /// <summary>
    /// Executes the Azure Service Bus queue-triggered function registered for
    /// <paramref name="queueName"/> with a pre-built <see cref="ServiceBusReceivedMessage"/>.
    /// Use this overload when you need full control over message metadata (subject, message-id,
    /// correlation-id, application properties, etc.).
    /// </summary>
    /// <typeparam name="T">
    /// Expected return type of the function (output binding).
    /// Use <c>object</c> when the function does not return a meaningful value.
    /// </typeparam>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="message">The pre-built message to pass directly to the function.</param>
    /// <returns>
    /// An <see cref="AzureServiceBusExecutionResult{T}"/> containing the strongly-typed return value
    /// (output binding) and the recorded message-settlement actions for assertion.
    /// </returns>
    Task<AzureServiceBusExecutionResult<T>> ExecuteQueueAsync<T>(
        string queueName,
        ServiceBusReceivedMessage message);

    // ── Batched queue execution ───────────────────────────────────────────────

    /// <summary>
    /// Executes an Azure Service Bus queue-triggered function configured with <c>IsBatched = true</c>
    /// with a batch of pre-built <see cref="ServiceBusReceivedMessage"/> instances.
    /// Use this overload when you need full control over individual message metadata.
    /// </summary>
    /// <typeparam name="T">
    /// Expected return type of the function (output binding).
    /// Use <c>object</c> when the function does not return a meaningful value.
    /// </typeparam>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="messages">The pre-built messages to pass directly to the function.</param>
    Task<AzureServiceBusExecutionResult<T>> ExecuteBatchQueueAsync<T>(
        string queueName,
        ServiceBusReceivedMessage[] messages);

    // ── Topic execution ───────────────────────────────────────────────────────

    /// <summary>
    /// Executes Azure Service Bus topic-triggered functions bound to <paramref name="topicName"/>
    /// with a pre-built <see cref="ServiceBusReceivedMessage"/>.
    /// Use this overload when you need full control over message metadata.
    /// </summary>
    /// <typeparam name="T">
    /// Expected return type of the function (output binding).
    /// Use <c>object</c> when the function does not return a meaningful value.
    /// </typeparam>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="message">The pre-built message to pass directly to the function.</param>
    /// <param name="subscriptionName">
    /// Optional subscription filter.
    /// <list type="bullet">
    ///   <item>
    ///     <term><see langword="null"/> (default)</term>
    ///     <description>All functions bound to the topic are executed regardless of subscription.</description>
    ///   </item>
    ///   <item>
    ///     <term>non-null</term>
    ///     <description>Only the function matching the given subscription name is executed.</description>
    ///   </item>
    /// </list>
    /// </param>
    /// <returns>
    /// An <see cref="AzureServiceBusExecutionResult{T}"/> from the last matching subscription executed.
    /// </returns>
    Task<AzureServiceBusExecutionResult<T>> ExecuteTopicAsync<T>(
        string topicName,
        ServiceBusReceivedMessage message,
        string? subscriptionName = null);

    // ── Batched topic execution ───────────────────────────────────────────────

    /// <summary>
    /// Executes an Azure Service Bus topic-triggered function configured with <c>IsBatched = true</c>
    /// with a batch of pre-built <see cref="ServiceBusReceivedMessage"/> instances.
    /// Use this overload when you need full control over individual message metadata.
    /// </summary>
    /// <typeparam name="T">
    /// Expected return type of the function (output binding).
    /// Use <c>object</c> when the function does not return a meaningful value.
    /// </typeparam>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="messages">The pre-built messages to pass directly to the function.</param>
    /// <param name="subscriptionName">Optional subscription filter.</param>
    Task<AzureServiceBusExecutionResult<T>> ExecuteBatchTopicAsync<T>(
        string topicName,
        ServiceBusReceivedMessage[] messages,
        string? subscriptionName = null);
}
