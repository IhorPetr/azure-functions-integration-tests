using System.Reflection;

namespace AzureFunctions.IntegrationTests.Models;

/// <summary>
/// Contains metadata about a discovered Azure Function with an Azure Service Bus trigger
/// </summary>
internal class AzureServiceBusFunctionInfo
{
    /// <summary>
    /// The type that contains the function method
    /// </summary>
    public required Type FunctionType { get; init; }

    /// <summary>
    /// The method that implements the function
    /// </summary>
    public required MethodInfo Method { get; init; }

    /// <summary>
    /// The queue name or topic name configured on the Azure Service Bus trigger attribute
    /// </summary>
    public required string EntityPath { get; init; }

    /// <summary>
    /// The subscription name when the trigger is bound to a topic subscription; null for queues
    /// </summary>
    public string? SubscriptionName { get; init; }

    /// <summary>
    /// <see langword="true"/> when the trigger is bound to a topic subscription;
    /// <see langword="false"/> when it is bound to a queue.
    /// </summary>
    public bool IsTopicTrigger { get; init; }

    /// <summary>
    /// The name of the function as defined in the [Function] attribute
    /// </summary>
    public required string FunctionName { get; init; }

    /// <summary>
    /// The parameter info for the Azure Service Bus trigger-decorated parameter
    /// </summary>
    public required ParameterInfo TriggerParameter { get; init; }

    /// <summary>
    /// <see langword="true"/> when the trigger parameter type is a collection of
    /// <see cref="Azure.Messaging.ServiceBus.ServiceBusReceivedMessage"/>
    /// (i.e. the function is configured for batched processing).
    /// </summary>
    public bool IsBatched { get; init; }

    /// <summary>
    /// <see langword="true"/> when the function method has a
    /// <see cref="Microsoft.Azure.Functions.Worker.ServiceBusSessionMessageActions"/> parameter,
    /// indicating it is bound to a session-enabled queue or topic subscription.
    /// </summary>
    public bool IsSessionsEnabled { get; init; }    
}