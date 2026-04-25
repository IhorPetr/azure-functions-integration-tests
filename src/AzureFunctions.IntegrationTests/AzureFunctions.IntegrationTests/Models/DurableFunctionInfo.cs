using System.Reflection;

namespace AzureFunctions.IntegrationTests.Models;

/// <summary>
/// Categorises the kind of Durable Functions trigger bound to a method parameter.
/// </summary>
public enum DurableTriggerKind
{
    /// <summary>An <c>[OrchestrationTrigger]</c> binding.</summary>
    Orchestration,

    /// <summary>An <c>[ActivityTrigger]</c> binding.</summary>
    Activity,

    /// <summary>An <c>[EntityTrigger]</c> binding.</summary>
    Entity,
}

/// <summary>
/// Contains metadata about a discovered Azure Function with a Durable Functions trigger.
/// </summary>
public class DurableFunctionInfo
{
    /// <summary>The type that contains the function method.</summary>
    public required Type FunctionType { get; init; }

    /// <summary>The method that implements the function.</summary>
    public required MethodInfo Method { get; init; }

    /// <summary>The name of the function as defined in the [Function] attribute.</summary>
    public required string FunctionName { get; init; }

    /// <summary>The parameter decorated with the Durable trigger attribute.</summary>
    public required ParameterInfo TriggerParameter { get; init; }

    /// <summary>Whether this is an orchestration, activity, or entity trigger.</summary>
    public required DurableTriggerKind Kind { get; init; }
}