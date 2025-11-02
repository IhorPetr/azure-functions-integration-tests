using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace AzureFunctions.IntegrationTests.Mocks;

/// <summary>
/// Simple mock implementation of FunctionContext for testing
/// </summary>
public class SimpleFunctionContext : FunctionContext
{
    private readonly SimpleTraceContext _traceContext = new();
    private readonly SimpleBindingContext _bindingContext = new();
    private readonly SimpleRetryContext _retryContext = new();
    private readonly SimpleFunctionDefinition _functionDefinition = new();
    private readonly SimpleInvocationFeatures _features = new();

    /// <summary>
    /// Initializes a new instance of SimpleFunctionContext
    /// </summary>
    /// <param name="serviceProvider">Optional service provider</param>
    public SimpleFunctionContext(IServiceProvider? serviceProvider = null)
    {
        InstanceServices = serviceProvider ?? new ServiceCollection().BuildServiceProvider();
    }

    /// <inheritdoc/>
    public override string InvocationId { get; } = Guid.NewGuid().ToString();

    /// <inheritdoc/>
    public override string FunctionId { get; } = "test-function";

    /// <inheritdoc/>
    public override TraceContext TraceContext => _traceContext;

    /// <inheritdoc/>
    public override BindingContext BindingContext => _bindingContext;

    /// <inheritdoc/>
    public override RetryContext RetryContext => _retryContext;

    /// <inheritdoc/>
    public override IServiceProvider InstanceServices { get; set; }

    /// <inheritdoc/>
    public override FunctionDefinition FunctionDefinition => _functionDefinition;

    /// <inheritdoc/>
    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

    /// <inheritdoc/>
    public override IInvocationFeatures Features => _features;
}
