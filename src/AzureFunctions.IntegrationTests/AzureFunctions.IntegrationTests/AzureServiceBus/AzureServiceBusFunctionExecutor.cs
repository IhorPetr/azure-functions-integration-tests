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
/// Executes Azure Functions with a <see cref="ServiceBusTriggerAttribute"/> bound
/// to either a <b>queue</b> or a <b>topic subscription</b>, enabling in-process integration
/// testing without a live Azure Service Bus namespace.
/// Obtain an instance via <c>FunctionAppFactory.CreateAzureServiceBusFunctionExecutor()</c>.
/// </summary>
internal sealed class AzureServiceBusFunctionExecutor : IAzureServiceBusFunctionExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, AzureServiceBusFunctionInfo> _queues;

    // topicName → all subscription functions registered for that topic
    private readonly Dictionary<string, List<AzureServiceBusFunctionInfo>> _topics;

    internal AzureServiceBusFunctionExecutor(
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

    // ── Queue execution ───────────────────────────────────────────────────────

    /// <summary>
    /// Executes the function whose <see cref="ServiceBusTriggerAttribute"/> is
    /// bound to <paramref name="queueName"/> with a pre-built <see cref="ServiceBusReceivedMessage"/>.
    /// Use this overload when you need full control over message metadata (headers, subject,
    /// message-id, correlation-id, etc.).
    /// </summary>
    /// <typeparam name="T">
    /// Expected return type of the function (output binding).
    /// Use <c>object</c> when the function does not return a meaningful value.
    /// </typeparam>
    /// <param name="queueName">Queue name configured on the trigger.</param>
    /// <param name="message">The pre-built message to pass directly to the function.</param>
    /// <returns>
    /// An <see cref="AzureServiceBusExecutionResult{T}"/> containing the strongly-typed return value
    /// and the recorded message actions for assertion.
    /// </returns>
    public async Task<AzureServiceBusExecutionResult<T>> ExecuteQueueAsync<T>(
        string queueName,
        ServiceBusReceivedMessage message)
    {
        if (!_queues.TryGetValue(queueName, out var info))
        {
            throw new InvalidOperationException(
                $"No queue-triggered function found for queue '{queueName}'. " +
                $"Available queues: {string.Join(", ", _queues.Keys)}");
        }

        return await ServiceBusMessageBuilder.ExecuteAsync<T>(info, message, null, _serviceProvider);
    }
    
    

    // ── Batched queue execution ───────────────────────────────────────────────

    /// <summary>
    /// Executes a function configured with <c>IsBatched = true</c> with a batch of
    /// pre-built <see cref="ServiceBusReceivedMessage"/> instances.
    /// Use this overload when you need full control over individual message metadata.
    /// </summary>
    /// <typeparam name="T">
    /// Expected return type of the function (output binding).
    /// Use <c>object</c> when the function does not return a meaningful value.
    /// </typeparam>
    /// <param name="queueName">Queue name configured on the trigger.</param>
    /// <param name="messages">The pre-built messages to pass directly to the function.</param>
    public async Task<AzureServiceBusExecutionResult<T>> ExecuteBatchQueueAsync<T>(
        string queueName,
        ServiceBusReceivedMessage[] messages)
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
                "Use ExecuteQueueAsync for single-message execution.");
        }

        return await ServiceBusMessageBuilder.ExecuteAsync<T>(info, messages[0], messages, _serviceProvider);
    }

    // ── Topic execution ───────────────────────────────────────────────────────

    /// <summary>
    /// Executes topic-triggered functions bound to <paramref name="topicName"/> with a
    /// pre-built <see cref="ServiceBusReceivedMessage"/>.
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
    ///     <description>All functions bound to the topic are executed, regardless of subscription name.</description>
    ///   </item>
    ///   <item>
    ///     <term>non-null</term>
    ///     <description>Only the function whose subscription name matches is executed.</description>
    ///   </item>
    /// </list>
    /// </param>
    /// <returns>
    /// An <see cref="AzureServiceBusExecutionResult{T}"/> from the last matching subscription executed.
    /// </returns>
    public async Task<AzureServiceBusExecutionResult<T>> ExecuteTopicAsync<T>(
        string topicName,
        ServiceBusReceivedMessage message,
        string? subscriptionName = null)
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
                    $"No function found for topic '{topicName}' with subscription '{subscriptionName}'. " +
                    $"Available subscriptions: {string.Join(", ", subscriptions.Select(s => s.SubscriptionName))}");
            }
            targets = [match];
        }

        AzureServiceBusExecutionResult<T>? lastResult = null;
        foreach (var target in targets)
            lastResult = await ServiceBusMessageBuilder.ExecuteAsync<T>(target, message, null, _serviceProvider);

        return lastResult!;
    }

    // ── Batched topic execution ───────────────────────────────────────────────

    /// <summary>
    /// Executes a topic function configured with <c>IsBatched = true</c> with a batch of
    /// pre-built <see cref="ServiceBusReceivedMessage"/> instances.
    /// Use this overload when you need full control over individual message metadata.
    /// </summary>
    /// <typeparam name="T">
    /// Expected return type of the function (output binding).
    /// Use <c>object</c> when the function does not return a meaningful value.
    /// </typeparam>
    /// <param name="topicName">Topic name configured on the trigger.</param>
    /// <param name="messages">The pre-built messages to pass directly to the function.</param>
    /// <param name="subscriptionName">Optional subscription filter.</param>
    public async Task<AzureServiceBusExecutionResult<T>> ExecuteBatchTopicAsync<T>(
        string topicName,
        ServiceBusReceivedMessage[] messages,
        string? subscriptionName = null)
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

        AzureServiceBusExecutionResult<T>? lastResult = null;
        foreach (var target in targets)
        {
            if (!target.IsBatched)
            {
                throw new InvalidOperationException(
                    $"Topic function '{target.FunctionName}' is not configured for batched processing.");
            }
            lastResult = await ServiceBusMessageBuilder.ExecuteAsync<T>(target, messages[0], messages, _serviceProvider);
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
    internal static async Task<AzureServiceBusExecutionResult<T>> ExecuteAsync<T>(
        AzureServiceBusFunctionInfo functionInfo,
        ServiceBusReceivedMessage message,
        ServiceBusReceivedMessage[]? batchMessages,
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
        T? returnValue = default;

        if (result is Task task)
        {
            await task;
            var resultProp = task.GetType().GetProperty("Result");
            if (resultProp != null && resultProp.PropertyType != typeof(void))
            {
                var rawValue = resultProp.GetValue(task);
                if (rawValue is T typed)
                    returnValue = typed;
            }
        }
        else if (result is T directlyTyped)
        {
            returnValue = directlyTyped;
        }

        return new AzureServiceBusExecutionResult<T>
        {
            ReturnValue = returnValue,
            MessageActions = messageActions,
            SessionMessageActions = sessionActions,
        };
    }

    private static object[] BuildParameters(
        AzureServiceBusFunctionInfo functionInfo,
        ServiceBusReceivedMessage message,
        ServiceBusReceivedMessage[]? batchMessages,
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