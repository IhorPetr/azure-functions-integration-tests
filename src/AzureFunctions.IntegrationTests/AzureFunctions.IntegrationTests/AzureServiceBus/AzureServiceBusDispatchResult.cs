using AzureFunctions.IntegrationTests.Mocks.AzureServiceBus;

namespace AzureFunctions.IntegrationTests.AzureServiceBus;

/// <summary>
/// The result of an Azure Service Bus function dispatch via <see cref="AzureServiceBusDispatcher"/>.
/// Provides access to the function's return value (output binding) and the recorded
/// message actions so tests can assert settlement calls.
/// </summary>
public class AzureServiceBusDispatchResult
{
    /// <summary>
    /// The raw value returned by the function, or <see langword="null"/> if the function
    /// returns <see cref="System.Threading.Tasks.Task"/> / <c>void</c>.
    /// Cast to the expected output binding type to assert on it.
    /// </summary>
    public object? ReturnValue { get; init; }

    /// <summary>
    /// Recorded message settlement actions (Complete, Abandon, DeadLetter, Defer).
    /// Always populated — even for session-enabled functions the settlement mock is here.
    /// </summary>
    public MockAzureServiceBusMessageActions MessageActions { get; init; } = null!;

    /// <summary>
    /// Recorded session operations (GetSessionState, SetSessionState, RenewSessionLock).
    /// Only non-null for session-enabled functions (those whose trigger includes a
    /// <see cref="Microsoft.Azure.Functions.Worker.ServiceBusSessionMessageActions"/> parameter).
    /// </summary>
    public MockAzureServiceBusSessionMessageActions? SessionMessageActions { get; init; }
}