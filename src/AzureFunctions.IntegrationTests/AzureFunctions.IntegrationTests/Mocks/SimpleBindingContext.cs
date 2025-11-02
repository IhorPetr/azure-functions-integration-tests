using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.Mocks;

/// <summary>
/// Simple mock implementation of BindingContext for testing
/// </summary>
public class SimpleBindingContext : BindingContext
{
    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object?> BindingData { get; } = new Dictionary<string, object?>();
}
