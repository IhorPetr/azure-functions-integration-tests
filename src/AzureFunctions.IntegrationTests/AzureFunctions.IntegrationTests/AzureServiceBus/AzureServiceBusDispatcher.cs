using System.Reflection;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AzureFunctions.IntegrationTests.Mocks;
using AzureFunctions.IntegrationTests.Mocks.AzureServiceBus;
using AzureFunctions.IntegrationTests.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace AzureFunctions.IntegrationTests.AzureServiceBus;


/// <summary>
/// Dispatches messages to Azure Functions with a <see cref="ServiceBusTriggerAttribute"/> bound
/// to either a <b>queue</b> or a <b>topic subscription</b>, enabling in-process integration
/// testing without a live Azure Service Bus namespace.
/// Obtain an instance via <c>FunctionAppFactory.CreateAzureServiceBusDispatcher()</c>.
/// </summary>
public class AzureServiceBusDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, AzureServiceBusFunctionInfo> _queues;

    // topicName → all subscription functions registered for that topic
    private readonly Dictionary<string, List<AzureServiceBusFunctionInfo>> _topics;

    internal AzureServiceBusDispatcher(
        IServiceProvider serviceProvider,
        IEnumerable<AzureServiceBusFunctionInfo> functions)
    {
        _serviceProvider = serviceProvider;
        _queues = new Dictionary<string, AzureServiceBusFunctionInfo>(StringComparer.OrdinalIgnoreCase);
        _topics = new Dictionary<string, List<AzureServiceBusFunctionInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var info in functions)
        {
            if (info.IsTopicTrigger)
            {
                if (!_topics.TryGetValue(info.EntityPath, out var list))
                {
                    list = new List<AzureServiceBusFunctionInfo>();
                    _topics[info.EntityPath] = list;
                }
                list.Add(info);
            }
            else
            {
                _queues[info.EntityPath] = info;
            }
        }
    }

    // ── Queue dispatch ───────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a message to the function whose <see cref="ServiceBusTriggerAttribute"/> is
    /// bound to <paramref name="queueName"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="queueName">Queue name configured on the trigger.</param>
    /// <param name="message">The message payload to send.</param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <see cref="ServiceBusReceivedMessage.Subject"/>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach.</param>
    /// <returns>
    /// A <see cref="AzureServiceBusDispatchResult"/> containing the function's return value (if any)
    /// and the recorded message actions for assertion.
    /// </returns>
    public Task<AzureServiceBusDispatchResult> DispatchToQueueAsync<TMessage>(
        string queueName,
        TMessage message,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null)
        => DispatchToQueueAsync(queueName, (object?)message, messageType, applicationProperties);

    /// <summary>
    /// Dispatches a message to the function whose <see cref="ServiceBusTriggerAttribute"/> is
    /// bound to <paramref name="queueName"/>.
    /// </summary>
    /// <param name="queueName">Queue name configured on the trigger.</param>
    /// <param name="message">The message payload to send.</param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <see cref="ServiceBusReceivedMessage.Subject"/>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach.</param>
    /// <returns>
    /// A <see cref="AzureServiceBusDispatchResult"/> containing the function's return value (if any)
    /// and the recorded message actions for assertion.
    /// </returns>
    public async Task<AzureServiceBusDispatchResult> DispatchToQueueAsync(
        string queueName,
        object? message,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null)
    {
        if (!_queues.TryGetValue(queueName, out var info))
        {
            throw new InvalidOperationException(
                $"No queue-triggered function found for queue '{queueName}'. " +
                $"Available queues: {string.Join(", ", _queues.Keys)}");
        }

        var sbMessage = ServiceBusMessageBuilder.Build(message, messageType, applicationProperties);
        return await ServiceBusMessageBuilder.InvokeAsync(info, sbMessage, null, _serviceProvider);
    }

    // ── Batched queue dispatch ───────────────────────────────────────────────

    /// <summary>
    /// Dispatches a batch of messages to a function configured with <c>IsBatched = true</c>.
    /// The entire batch is passed as <c>IReadOnlyList&lt;ServiceBusReceivedMessage&gt;</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="queueName">Queue name configured on the trigger.</param>
    /// <param name="messages">The batch of payloads to dispatch.</param>
    /// <param name="messageType">Optional subject applied to every message in the batch.</param>
    /// <param name="applicationProperties">Optional application properties applied to every message.</param>
    public Task<AzureServiceBusDispatchResult> DispatchBatchToQueueAsync<TMessage>(
        string queueName,
        IReadOnlyList<TMessage> messages,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null)
        => DispatchBatchToQueueAsync(queueName, messages.Cast<object?>().ToList(), messageType, applicationProperties);

    /// <summary>
    /// Dispatches a batch of messages to a function configured with <c>IsBatched = true</c>.
    /// </summary>
    public async Task<AzureServiceBusDispatchResult> DispatchBatchToQueueAsync(
        string queueName,
        IReadOnlyList<object?> messages,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null)
    {
        if (!_queues.TryGetValue(queueName, out var info))
        {
            throw new InvalidOperationException(
                $"No queue-triggered function found for queue '{queueName}'. " +
                $"Available queues: {string.Join(", ", _queues.Keys)}");
        }

        if (!info.IsBatched)
        {
            throw new InvalidOperationException(
                $"Queue function '{info.FunctionName}' is not configured for batched processing. " +
                "Use DispatchToQueueAsync for single-message dispatch.");
        }

        var batch = messages
            .Select(m => ServiceBusMessageBuilder.Build(m, messageType, applicationProperties))
            .ToList();

        return await ServiceBusMessageBuilder.InvokeAsync(info, batch[0], batch, _serviceProvider);
    }

    // ── Topic dispatch ───────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a message to topic-triggered functions bound to <paramref name="topicName"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="message">The message payload to send.</param>
    /// <param name="subscriptionName">
    /// Optional subscription filter.
    /// <list type="bullet">
    ///   <item>
    ///     <term><see langword="null"/> (default)</term>
    ///     <description>All functions bound to the topic are invoked, regardless of subscription name.</description>
    ///   </item>
    ///   <item>
    ///     <term>non-null</term>
    ///     <description>Only the function whose subscription name matches is invoked.</description>
    ///   </item>
    /// </list>
    /// </param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <see cref="ServiceBusReceivedMessage.Subject"/>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach.</param>
    /// <returns>
    /// A <see cref="AzureServiceBusDispatchResult"/> from the last matching subscription invoked.
    /// When multiple subscriptions match, use <see cref="DispatchToTopicAsync(string,object?,string?,string?,IDictionary{string,object}?)"/>
    /// and target a specific subscription to get per-invocation results.
    /// </returns>
    public Task<AzureServiceBusDispatchResult> DispatchToTopicAsync<TMessage>(
        string topicName,
        TMessage message,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null)
        => DispatchToTopicAsync(topicName, (object?)message, subscriptionName, messageType, applicationProperties);

    /// <summary>
    /// Dispatches a message to topic-triggered functions bound to <paramref name="topicName"/>.
    /// </summary>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="message">The message payload to send.</param>
    /// <param name="subscriptionName">
    /// Optional subscription filter.
    /// <list type="bullet">
    ///   <item>
    ///     <term><see langword="null"/> (default)</term>
    ///     <description>All functions bound to the topic are invoked, regardless of subscription name.</description>
    ///   </item>
    ///   <item>
    ///     <term>non-null</term>
    ///     <description>Only the function whose subscription name matches is invoked.</description>
    ///   </item>
    /// </list>
    /// </param>
    /// <param name="messageType">
    /// Optional subject / message-type discriminator written to
    /// <see cref="ServiceBusReceivedMessage.Subject"/>.
    /// </param>
    /// <param name="applicationProperties">Optional application properties to attach.</param>
    public async Task<AzureServiceBusDispatchResult> DispatchToTopicAsync(
        string topicName,
        object? message,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null)
    {
        if (!_topics.TryGetValue(topicName, out var subscriptions))
        {
            throw new InvalidOperationException(
                $"No topic-triggered function found for topic '{topicName}'. " +
                $"Available topics: {string.Join(", ", _topics.Keys)}");
        }

        IEnumerable<AzureServiceBusFunctionInfo> targets;

        if (subscriptionName is null)
        {
            // No filter → invoke all registered subscriptions for this topic
            targets = subscriptions;
        }
        else
        {
            var match = subscriptions.FirstOrDefault(s =>
                string.Equals(s.SubscriptionName, subscriptionName, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                throw new InvalidOperationException(
                    $"No function found for topic '{topicName}' with subscription '{subscriptionName}'. " +
                    $"Available subscriptions: {string.Join(", ", subscriptions.Select(s => s.SubscriptionName))}");
            }

            targets = [match];
        }

        var sbMessage = ServiceBusMessageBuilder.Build(message, messageType, applicationProperties);

        AzureServiceBusDispatchResult? lastResult = null;
        foreach (var target in targets)
            lastResult = await ServiceBusMessageBuilder.InvokeAsync(target, sbMessage, null, _serviceProvider);

        return lastResult!;
    }

    // ── Batched topic dispatch ───────────────────────────────────────────────

    /// <summary>
    /// Dispatches a batch of messages to a topic function configured with <c>IsBatched = true</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message payload type.</typeparam>
    public Task<AzureServiceBusDispatchResult> DispatchBatchToTopicAsync<TMessage>(
        string topicName,
        IReadOnlyList<TMessage> messages,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null)
        => DispatchBatchToTopicAsync(topicName, messages.Cast<object?>().ToList(), subscriptionName, messageType, applicationProperties);

    /// <summary>
    /// Dispatches a batch of messages to a topic function configured with <c>IsBatched = true</c>.
    /// </summary>
    public async Task<AzureServiceBusDispatchResult> DispatchBatchToTopicAsync(
        string topicName,
        IReadOnlyList<object?> messages,
        string? subscriptionName = null,
        string? messageType = null,
        IDictionary<string, object>? applicationProperties = null)
    {
        if (!_topics.TryGetValue(topicName, out var subscriptions))
        {
            throw new InvalidOperationException(
                $"No topic-triggered function found for topic '{topicName}'. " +
                $"Available topics: {string.Join(", ", _topics.Keys)}");
        }

        IEnumerable<AzureServiceBusFunctionInfo> targets;
        if (subscriptionName is null)
        {
            targets = subscriptions;
        }
        else
        {
            var match = subscriptions.FirstOrDefault(s =>
                string.Equals(s.SubscriptionName, subscriptionName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                throw new InvalidOperationException(
                    $"No function found for topic '{topicName}' with subscription '{subscriptionName}'.");
            }
            targets = [match];
        }

        var batch = messages
            .Select(m => ServiceBusMessageBuilder.Build(m, messageType, applicationProperties))
            .ToList();

        AzureServiceBusDispatchResult? lastResult = null;
        foreach (var target in targets)
        {
            if (!target.IsBatched)
            {
                throw new InvalidOperationException(
                    $"Topic function '{target.FunctionName}' is not configured for batched processing.");
            }
            lastResult = await ServiceBusMessageBuilder.InvokeAsync(target, batch[0], batch, _serviceProvider);
        }

        return lastResult!;
    }
}

/// <summary>
/// Internal helper for constructing <see cref="ServiceBusReceivedMessage"/> instances and
/// invoking discovered Azure Service Bus triggered functions in-process.
/// </summary>
internal static class ServiceBusMessageBuilder
{
    internal static ServiceBusReceivedMessage Build(
        object? payload,
        string? subject,
        IDictionary<string, object>? applicationProperties)
    {
        var body = payload is null
            ? BinaryData.Empty
            : BinaryData.FromObjectAsJson(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        var props = new Dictionary<string, object>(
            applicationProperties ?? new Dictionary<string, object>());

        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: body,
            subject: subject,
            properties: props,
            messageId: Guid.NewGuid().ToString());
    }

    internal static async Task<AzureServiceBusDispatchResult> InvokeAsync(
        AzureServiceBusFunctionInfo functionInfo,
        ServiceBusReceivedMessage message,
        IReadOnlyList<ServiceBusReceivedMessage>? batchMessages,
        IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var instance = ActivatorUtilities.CreateInstance(
            scope.ServiceProvider, functionInfo.FunctionType);

        var context = new SimpleFunctionContext(scope.ServiceProvider);

        var messageActions = new MockAzureServiceBusMessageActions();
        var sessionActions = functionInfo.IsSessionsEnabled
            ? new MockAzureServiceBusSessionMessageActions()
            : null;

        var parameters = BuildParameters(
            functionInfo, message, batchMessages, messageActions, sessionActions, context);

        var result = functionInfo.Method.Invoke(instance, parameters);
        object? returnValue = null;

        if (result is Task task)
        {
            await task;
            var resultProp = task.GetType().GetProperty("Result");
            if (resultProp != null && resultProp.PropertyType != typeof(void))
                returnValue = resultProp.GetValue(task);
        }
        else
        {
            returnValue = result;
        }

        return new AzureServiceBusDispatchResult
        {
            ReturnValue = returnValue,
            MessageActions = messageActions,
            SessionMessageActions = sessionActions,
        };
    }

    private static object[] BuildParameters(
        AzureServiceBusFunctionInfo functionInfo,
        ServiceBusReceivedMessage message,
        IReadOnlyList<ServiceBusReceivedMessage>? batchMessages,
        MockAzureServiceBusMessageActions messageActions,
        MockAzureServiceBusSessionMessageActions? sessionActions,
        FunctionContext context)
    {
        var methodParams = functionInfo.Method.GetParameters();
        var values = new object[methodParams.Length];

        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];
            var hasTrigger = param.GetCustomAttribute<ServiceBusTriggerAttribute>() != null;

            if (param.ParameterType == typeof(ServiceBusReceivedMessage) && hasTrigger)
            {
                // Single raw message with trigger attribute
                values[i] = message;
            }
            else if (param.ParameterType == typeof(IReadOnlyList<ServiceBusReceivedMessage>))
            {
                // Batched Azure Service Bus trigger: IReadOnlyList<ServiceBusReceivedMessage>
                values[i] = batchMessages ?? (IReadOnlyList<ServiceBusReceivedMessage>)[message];
            }
            else if (param.ParameterType == typeof(ServiceBusReceivedMessage[]))
            {
                // Batched Azure Service Bus trigger: ServiceBusReceivedMessage[]
                values[i] = (batchMessages ?? [message]).ToArray();
            }
            else if (param.ParameterType == typeof(ServiceBusReceivedMessage) && !hasTrigger)
            {
                // Non-trigger raw message (first of batch or single)
                values[i] = message;
            }
            else if (param.ParameterType == typeof(FunctionContext))
            {
                values[i] = context;
            }
            else if (param.ParameterType == typeof(ServiceBusSessionMessageActions))
            {
                values[i] = sessionActions
                    ?? throw new InvalidOperationException(
                        $"Function parameter '{param.Name}' requires a session-enabled function. " +
                        "Ensure the function is detected as session-enabled.");
            }
            else if (param.ParameterType == typeof(ServiceBusMessageActions))
            {
                values[i] = messageActions;
            }
            else if (hasTrigger)
            {
                // Trigger-decorated parameter with a typed body (e.g. MyModel, string)
                values[i] = DeserializeBody(message.Body, param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                values[i] = param.DefaultValue!;
            }
            else
            {
                var created = Activator.CreateInstance(param.ParameterType);
                values[i] = created
                    ?? throw new InvalidOperationException(
                        $"Cannot create instance of parameter type '{param.ParameterType.FullName}'.");
            }
        }

        return values;
    }

    private static object DeserializeBody(BinaryData body, Type targetType)
    {
        var json = body.ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            return Activator.CreateInstance(targetType)
                ?? throw new InvalidOperationException(
                    $"Cannot create default instance of '{targetType.FullName}'.");
        }

        return JsonSerializer.Deserialize(json, targetType, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException(
            $"Failed to deserialize message body to '{targetType.FullName}'.");
    }
}