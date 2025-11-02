using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.Mocks;

/// <summary>
/// Simple mock implementation of TraceContext for testing
/// </summary>
public class SimpleTraceContext : TraceContext
{
    /// <inheritdoc/>
    public override string TraceParent { get; } = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    /// <inheritdoc/>
    public override string TraceState { get; } = string.Empty;
}
