using Microsoft.DurableTask;

namespace AzureFunctions.IntegrationTests.Mocks.Durable;

/// <summary>
/// Minimal concrete implementation of <see cref="TaskActivityContext"/> for use in
/// in-process integration tests. Provides the function name and a synthetic instance ID;
/// all other state is supplied by the test via the activity's method parameters.
/// </summary>
public class MockTaskActivityContext : TaskActivityContext
{
    /// <summary>
    /// Initialises a new <see cref="MockTaskActivityContext"/>.
    /// </summary>
    /// <param name="functionName">The name of the activity function being tested.</param>
    /// <param name="instanceId">The synthetic orchestration instance ID to expose.</param>
    public MockTaskActivityContext(string functionName, string? instanceId = null)
    {
        Name = functionName;
        InstanceId = instanceId ?? Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public override TaskName Name { get; }

    /// <inheritdoc/>
    public override string InstanceId { get; }
}