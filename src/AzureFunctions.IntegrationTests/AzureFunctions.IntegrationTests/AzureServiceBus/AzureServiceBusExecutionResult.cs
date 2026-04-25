using AzureFunctions.IntegrationTests.Mocks.AzureServiceBus;

namespace AzureFunctions.IntegrationTests.AzureServiceBus;

/// <summary>
/// The result of an Azure Service Bus function execution via <see cref="AzureServiceBusFunctionExecutor"/>.
/// Provides strongly-typed access to the function's return value (output binding) and the recorded
/// message actions so tests can assert settlement calls without casting.
/// </summary>
/// <typeparam name="T">
/// The expected return type of the Azure Service Bus triggered function.
/// Use <c>object</c> when the function returns <see cref="System.Threading.Tasks.Task"/> / <c>void</c>
/// or when the return type is not relevant to the test.
/// </typeparam>
public class AzureServiceBusExecutionResult<T>
{
    /// <summary>
    /// The strongly-typed value returned by the function, or <see langword="null"/> if the function
    /// returns <see cref="System.Threading.Tasks.Task"/> / <c>void</c>, or if the actual return value
    /// could not be cast to <typeparamref name="T"/>.
    /// </summary>
    public T? ReturnValue { get; init; }

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