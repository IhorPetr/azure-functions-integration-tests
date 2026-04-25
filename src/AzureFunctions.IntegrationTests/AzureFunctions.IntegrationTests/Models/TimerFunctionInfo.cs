using System.Reflection;

namespace AzureFunctions.IntegrationTests.Models;

/// <summary>
/// Contains metadata about a discovered Azure Function with a <see cref="Microsoft.Azure.Functions.Worker.TimerTriggerAttribute"/> binding.
/// </summary>
public class TimerFunctionInfo
{
    /// <summary>The type that contains the function method.</summary>
    public required Type FunctionType { get; init; }

    /// <summary>The method that implements the function.</summary>
    public required MethodInfo Method { get; init; }

    /// <summary>The name of the function as declared in <c>[Function("…")]</c>.</summary>
    public required string FunctionName { get; init; }

    /// <summary>The CRON expression or time-span string configured on the trigger.</summary>
    public required string Schedule { get; init; }
}