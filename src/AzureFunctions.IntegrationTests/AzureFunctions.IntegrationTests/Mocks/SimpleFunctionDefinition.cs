using System.Collections.Immutable;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.Mocks;

/// <summary>
/// Simple mock implementation of FunctionDefinition for testing
/// </summary>
public class SimpleFunctionDefinition : FunctionDefinition
{
    /// <inheritdoc/>
    public override ImmutableArray<FunctionParameter> Parameters { get; } = ImmutableArray<FunctionParameter>.Empty;

    /// <inheritdoc/>
    public override string PathToAssembly { get; } = string.Empty;

    /// <inheritdoc/>
    public override string EntryPoint { get; } = "TestFunction";

    /// <inheritdoc/>
    public override string Id { get; } = "test-function-id";

    /// <inheritdoc/>
    public override string Name { get; } = "TestFunction";

    /// <inheritdoc/>
    public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } =
        ImmutableDictionary<string, BindingMetadata>.Empty;

    /// <inheritdoc/>
    public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } =
        ImmutableDictionary<string, BindingMetadata>.Empty;
}
