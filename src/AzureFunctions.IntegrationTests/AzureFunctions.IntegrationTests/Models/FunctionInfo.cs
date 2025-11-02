using System.Reflection;

namespace AzureFunctions.IntegrationTests.Models;

/// <summary>
/// Contains metadata about a discovered Azure Function
/// </summary>
public class FunctionInfo
{
    /// <summary>
    /// The type that contains the function method
    /// </summary>
    public required Type FunctionType { get; init; }

    /// <summary>
    /// The method that implements the function
    /// </summary>
    public required MethodInfo Method { get; init; }

    /// <summary>
    /// The HTTP route for this function (e.g., "user/{id}")
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// The HTTP methods supported by this function (e.g., GET, POST, PUT, DELETE)
    /// </summary>
    public required string[] HttpMethods { get; init; }

    /// <summary>
    /// The name of the function as defined in the [Function] attribute
    /// </summary>
    public required string FunctionName { get; init; }
}
