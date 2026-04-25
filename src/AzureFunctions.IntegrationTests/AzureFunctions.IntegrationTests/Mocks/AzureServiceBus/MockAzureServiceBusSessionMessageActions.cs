using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.Mocks.AzureServiceBus;

/// <summary>
/// An in-memory mock of <see cref="ServiceBusSessionMessageActions"/> for use in integration tests.
/// Session-state operations (<c>GetSessionStateAsync</c>, <c>SetSessionStateAsync</c>) and
/// <c>RenewSessionLockAsync</c> are tracked in-memory so tests can assert against them without
/// a live Azure Service Bus connection.
/// </summary>
public class MockAzureServiceBusSessionMessageActions : ServiceBusSessionMessageActions
{
    /// <summary>
    /// The current session state. Starts as <see langword="null"/>; updated by
    /// <see cref="SetSessionStateAsync"/>.
    /// </summary>
    public BinaryData? SessionState { get; private set; }

    /// <summary>Tracks whether <c>RenewSessionLockAsync</c> was called.</summary>
    public bool SessionLockRenewed { get; private set; }

    /// <inheritdoc/>
    public override Task<BinaryData> GetSessionStateAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(SessionState ?? BinaryData.Empty);

    /// <summary>Records the new session state and makes it accessible via <see cref="SessionState"/>.</summary>
    public override Task SetSessionStateAsync(
        BinaryData sessionState,
        CancellationToken cancellationToken = default)
    {
        SessionState = sessionState;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task RenewSessionLockAsync(
        CancellationToken cancellationToken = default)
    {
        SessionLockRenewed = true;
        return Task.CompletedTask;
    }
}