using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.Mocks;

/// <summary>
/// Simple mock implementation of RetryContext for testing
/// </summary>
public class SimpleRetryContext : RetryContext
{
    /// <inheritdoc/>
    public override int RetryCount { get; } = 0;

    /// <inheritdoc/>
    public override int MaxRetryCount { get; } = 0;
}
