using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.Mocks.AzureServiceBus;

/// <summary>
/// An in-memory mock of <see cref="ServiceBusMessageActions"/> for use in integration tests.
/// All settlement calls (<c>CompleteMessageAsync</c>, <c>AbandonMessageAsync</c>, etc.) are
/// recorded in the corresponding collections so tests can assert against them without a live
/// Azure Service Bus connection.
/// </summary>
public class MockAzureServiceBusMessageActions : ServiceBusMessageActions
{
    private readonly List<ServiceBusReceivedMessage> _completed = new();
    private readonly List<(ServiceBusReceivedMessage Message, IDictionary<string, object>? Properties)> _abandoned = new();
    private readonly List<(ServiceBusReceivedMessage Message, string? Reason, string? Description, IDictionary<string, object>? Properties)> _deadLettered = new();
    private readonly List<(ServiceBusReceivedMessage Message, IDictionary<string, object>? Properties)> _deferred = new();

    /// <summary>Messages for which <c>CompleteMessageAsync</c> was called.</summary>
    public IReadOnlyList<ServiceBusReceivedMessage> CompletedMessages => _completed;

    /// <summary>Messages for which <c>AbandonMessageAsync</c> was called, with any properties to modify.</summary>
    public IReadOnlyList<(ServiceBusReceivedMessage Message, IDictionary<string, object>? Properties)> AbandonedMessages => _abandoned;

    /// <summary>Messages for which <c>DeadLetterMessageAsync</c> was called.</summary>
    public IReadOnlyList<(ServiceBusReceivedMessage Message, string? Reason, string? Description, IDictionary<string, object>? Properties)> DeadLetteredMessages => _deadLettered;

    /// <summary>Messages for which <c>DeferMessageAsync</c> was called.</summary>
    public IReadOnlyList<(ServiceBusReceivedMessage Message, IDictionary<string, object>? Properties)> DeferredMessages => _deferred;

    /// <inheritdoc/>
    public override Task CompleteMessageAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
    {
        _completed.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task AbandonMessageAsync(
        ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        _abandoned.Add((message, propertiesToModify));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task DeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        Dictionary<string, object>? propertiesToModify = null,
        string? deadLetterReason = null,
        string? deadLetterErrorDescription = null,
        CancellationToken cancellationToken = default)
    {
        _deadLettered.Add((message, deadLetterReason, deadLetterErrorDescription, propertiesToModify));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task DeferMessageAsync(
        ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        _deferred.Add((message, propertiesToModify));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task RenewMessageLockAsync(
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
