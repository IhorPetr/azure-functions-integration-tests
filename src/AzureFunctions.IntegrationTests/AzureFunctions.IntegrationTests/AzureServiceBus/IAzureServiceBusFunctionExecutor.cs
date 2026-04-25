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
/// </remarks>
public interface IAzureServiceBusFunctionExecutor
{
    // ── Queue execution ───────────────────────────────────────────────────────

    /// <summary>
    /// Executes a single typed message against the Azure Service Bus queue-triggered function
    /// registered for <paramref name="queueName"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="message">The message payload to serialize and pass to the function.</param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <c>ServiceBusReceivedMessage.Subject</c>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach to the message.</param>
    /// <returns>
    /// An <see cref="AzureServiceBusExecutionResult"/> containing the function's return value
    /// (output binding) and the recorded message-settlement actions for assertion.
    /// </returns>
    Task<AzureServiceBusExecutionResult> ExecuteQueueAsync<TMessage>(
        string queueName,
        TMessage message,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    /// <summary>
    /// Executes a single untyped message against the Azure Service Bus queue-triggered function
    /// registered for <paramref name="queueName"/>.
    /// </summary>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="message">The message payload to serialize and pass to the function.</param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <c>ServiceBusReceivedMessage.Subject</c>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach to the message.</param>
    /// <returns>
    /// An <see cref="AzureServiceBusExecutionResult"/> containing the function's return value
    /// (output binding) and the recorded message-settlement actions for assertion.
    /// </returns>
    Task<AzureServiceBusExecutionResult> ExecuteQueueAsync(
        string queueName,
        object? message,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    // ── Batched queue execution ───────────────────────────────────────────────

    /// <summary>
    /// Executes a batch of typed messages against an Azure Service Bus queue-triggered function
    /// configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="messages">The batch of payloads to pass to the function.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    Task<AzureServiceBusExecutionResult> ExecuteBatchQueueAsync<TMessage>(
        string queueName,
        IReadOnlyList<TMessage> messages,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    /// <summary>
    /// Executes a batch of untyped messages against an Azure Service Bus queue-triggered function
    /// configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="messages">The batch of payloads to pass to the function.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    Task<AzureServiceBusExecutionResult> ExecuteBatchQueueAsync(
        string queueName,
        IReadOnlyList<object?> messages,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    // ── Topic execution ───────────────────────────────────────────────────────

    /// <summary>
    /// Executes a single typed message against Azure Service Bus topic-triggered functions
    /// bound to <paramref name="topicName"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="message">The message payload to serialize and pass to the function.</param>
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
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <c>ServiceBusReceivedMessage.Subject</c>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach to the message.</param>
    /// <returns>
    /// An <see cref="AzureServiceBusExecutionResult"/> from the last matching subscription executed.
    /// </returns>
    Task<AzureServiceBusExecutionResult> ExecuteTopicAsync<TMessage>(
        string topicName,
        TMessage message,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    /// <summary>
    /// Executes a single untyped message against Azure Service Bus topic-triggered functions
    /// bound to <paramref name="topicName"/>.
    /// </summary>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="message">The message payload to serialize and pass to the function.</param>
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
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <c>ServiceBusReceivedMessage.Subject</c>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach to the message.</param>
    Task<AzureServiceBusExecutionResult> ExecuteTopicAsync(
        string topicName,
        object? message,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    // ── Batched topic execution ───────────────────────────────────────────────

    /// <summary>
    /// Executes a batch of typed messages against an Azure Service Bus topic-triggered function
    /// configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="messages">The batch of payloads to pass to the function.</param>
    /// <param name="subscriptionName">Optional subscription filter.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    Task<AzureServiceBusExecutionResult> ExecuteBatchTopicAsync<TMessage>(
        string topicName,
        IReadOnlyList<TMessage> messages,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    /// <summary>
    /// Executes a batch of untyped messages against an Azure Service Bus topic-triggered function
    /// configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="messages">The batch of payloads to pass to the function.</param>
    /// <param name="subscriptionName">Optional subscription filter.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    Task<AzureServiceBusExecutionResult> ExecuteBatchTopicAsync(
        string topicName,
        IReadOnlyList<object?> messages,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);
}

