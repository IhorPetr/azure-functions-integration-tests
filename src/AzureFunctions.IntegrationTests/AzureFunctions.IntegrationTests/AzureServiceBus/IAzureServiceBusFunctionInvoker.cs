namespace AzureFunctions.IntegrationTests.AzureServiceBus;

/// <summary>
/// Abstraction for dispatching messages to Azure Service Bus triggered Azure Functions
/// in-process during integration tests, without requiring a live Azure Service Bus namespace.
/// </summary>
/// <remarks>
/// Obtain a concrete instance via
/// <c>FunctionAppFactory&lt;TEntryPoint&gt;.CreateAzureServiceBusFunctionInvoker()</c>.
/// The interface can be used in test helpers or custom factory wrappers so that the
/// dispatcher can be substituted with a test double if needed.
/// </remarks>
public interface IAzureServiceBusFunctionInvoker
{
    // ── Queue dispatch ───────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a single typed message to the Azure Service Bus queue-triggered function
    /// registered for <paramref name="queueName"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="message">The message payload to serialize and dispatch.</param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <c>ServiceBusReceivedMessage.Subject</c>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach to the message.</param>
    /// <returns>
    /// An <see cref="AzureServiceBusDispatchResult"/> containing the function's return value
    /// (output binding) and the recorded message-settlement actions for assertion.
    /// </returns>
    Task<AzureServiceBusDispatchResult> InvokeQueueAsync<TMessage>(
        string queueName,
        TMessage message,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    /// <summary>
    /// Dispatches a single untyped message to the Azure Service Bus queue-triggered function
    /// registered for <paramref name="queueName"/>.
    /// </summary>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="message">The message payload to serialize and dispatch.</param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <c>ServiceBusReceivedMessage.Subject</c>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach to the message.</param>
    /// <returns>
    /// An <see cref="AzureServiceBusDispatchResult"/> containing the function's return value
    /// (output binding) and the recorded message-settlement actions for assertion.
    /// </returns>
    Task<AzureServiceBusDispatchResult> InvokeQueueAsync(
        string queueName,
        object? message,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    // ── Batched queue dispatch ───────────────────────────────────────────────

    /// <summary>
    /// Dispatches a batch of typed messages to an Azure Service Bus queue-triggered function
    /// configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="messages">The batch of payloads to dispatch.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    Task<AzureServiceBusDispatchResult> InvokeBatchQueueAsync<TMessage>(
        string queueName,
        IReadOnlyList<TMessage> messages,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    /// <summary>
    /// Dispatches a batch of untyped messages to an Azure Service Bus queue-triggered function
    /// configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <param name="queueName">Queue name configured on the trigger (after env-var resolution).</param>
    /// <param name="messages">The batch of payloads to dispatch.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    Task<AzureServiceBusDispatchResult> InvokeBatchQueueAsync(
        string queueName,
        IReadOnlyList<object?> messages,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    // ── Topic dispatch ───────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a single typed message to Azure Service Bus topic-triggered functions
    /// bound to <paramref name="topicName"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="message">The message payload to serialize and dispatch.</param>
    /// <param name="subscriptionName">
    /// Optional subscription filter.
    /// <list type="bullet">
    ///   <item>
    ///     <term><see langword="null"/> (default)</term>
    ///     <description>All functions bound to the topic are invoked regardless of subscription.</description>
    ///   </item>
    ///   <item>
    ///     <term>non-null</term>
    ///     <description>Only the function matching the given subscription name is invoked.</description>
    ///   </item>
    /// </list>
    /// </param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <c>ServiceBusReceivedMessage.Subject</c>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach to the message.</param>
    /// <returns>
    /// An <see cref="AzureServiceBusDispatchResult"/> from the last matching subscription invoked.
    /// </returns>
    Task<AzureServiceBusDispatchResult> InvokeTopicAsync<TMessage>(
        string topicName,
        TMessage message,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    /// <summary>
    /// Dispatches a single untyped message to Azure Service Bus topic-triggered functions
    /// bound to <paramref name="topicName"/>.
    /// </summary>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="message">The message payload to serialize and dispatch.</param>
    /// <param name="subscriptionName">
    /// Optional subscription filter.
    /// <list type="bullet">
    ///   <item>
    ///     <term><see langword="null"/> (default)</term>
    ///     <description>All functions bound to the topic are invoked regardless of subscription.</description>
    ///   </item>
    ///   <item>
    ///     <term>non-null</term>
    ///     <description>Only the function matching the given subscription name is invoked.</description>
    ///   </item>
    /// </list>
    /// </param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <c>ServiceBusReceivedMessage.Subject</c>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach to the message.</param>
    Task<AzureServiceBusDispatchResult> InvokeTopicAsync(
        string topicName,
        object? message,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    // ── Batched topic dispatch ───────────────────────────────────────────────

    /// <summary>
    /// Dispatches a batch of typed messages to an Azure Service Bus topic-triggered function
    /// configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="messages">The batch of payloads to dispatch.</param>
    /// <param name="subscriptionName">Optional subscription filter.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    Task<AzureServiceBusDispatchResult> InvokeBatchTopicAsync<TMessage>(
        string topicName,
        IReadOnlyList<TMessage> messages,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);

    /// <summary>
    /// Dispatches a batch of untyped messages to an Azure Service Bus topic-triggered function
    /// configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="messages">The batch of payloads to dispatch.</param>
    /// <param name="subscriptionName">Optional subscription filter.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    Task<AzureServiceBusDispatchResult> InvokeBatchTopicAsync(
        string topicName,
        IReadOnlyList<object?> messages,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null);
}

