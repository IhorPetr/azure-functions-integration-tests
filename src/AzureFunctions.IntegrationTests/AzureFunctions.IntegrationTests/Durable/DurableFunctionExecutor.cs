using System.Text.Json;
using AzureFunctions.IntegrationTests.Mocks;
using AzureFunctions.IntegrationTests.Mocks.Durable;
using AzureFunctions.IntegrationTests.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;

namespace AzureFunctions.IntegrationTests.Durable;

/// <summary>
/// Executes Durable Functions orchestrators and activities in-process during integration tests,
/// without connecting to a live Durable Task hub.
/// Obtain an instance via <c>FunctionAppFactory.CreateDurableFunctionExecutor()</c>.
/// </summary>
internal sealed class DurableFunctionExecutor : IDurableFunctionExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, DurableFunctionInfo> _functions;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal DurableFunctionExecutor(
        IServiceProvider serviceProvider,
        IEnumerable<DurableFunctionInfo> functions)
    {
        _serviceProvider = serviceProvider;
        _functions = new Dictionary<string, DurableFunctionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in functions)
            _functions[info.FunctionName] = info;
    }

    // ── Activity execution ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<TResult?> ExecuteActivityAsync<TInput, TResult>(
        string functionName, TInput input, string? instanceId = null)
        => ExecuteActivityAsync<TResult>(functionName, (object?)input, instanceId);

    /// <inheritdoc/>
    public async Task ExecuteActivityAsync<TInput>(
        string functionName, TInput input, string? instanceId = null)
        => await ExecuteActivityAsync<object>(functionName, (object?)input, instanceId);

    /// <inheritdoc/>
    public async Task<TResult?> ExecuteActivityAsync<TResult>(
        string functionName, object? input = null, string? instanceId = null)
    {
        var info = GetFunction(functionName, DurableTriggerKind.Activity);
        var context = new MockTaskActivityContext(functionName, instanceId);
        var result = await InvokeAsync(info, context, input);
        return ConvertResult<TResult>(result);
    }

    // ── Orchestrator execution ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TResult?> ExecuteOrchestratorAsync<TResult>(
        string functionName, MockTaskOrchestrationContext context)
    {
        var info = GetFunction(functionName, DurableTriggerKind.Orchestration);
        var result = await InvokeAsync(info, context, input: null);
        return ConvertResult<TResult>(result);
    }

    /// <inheritdoc/>
    public async Task ExecuteOrchestratorAsync(
        string functionName, MockTaskOrchestrationContext context)
        => await InvokeAsync(GetFunction(functionName, DurableTriggerKind.Orchestration), context, input: null);

    // ── Internals ─────────────────────────────────────────────────────────────

    private DurableFunctionInfo GetFunction(string functionName, DurableTriggerKind kind)
    {
        if (!_functions.TryGetValue(functionName, out var info))
        {
            var available = string.Join(", ", _functions.Keys);
            throw new InvalidOperationException(
                $"No {kind} function found with name '{functionName}'. " +
                $"Available Durable functions: {available}");
        }

        if (info.Kind != kind)
        {
            throw new InvalidOperationException(
                $"Function '{functionName}' is a {info.Kind} trigger, not a {kind} trigger.");
        }

        return info;
    }

    private async Task<object?> InvokeAsync(DurableFunctionInfo info, object context, object? input)
    {
        using var scope = _serviceProvider.CreateScope();
        var instance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, info.FunctionType);
        var functionContext = new SimpleFunctionContext(scope.ServiceProvider);

        var parameters = BuildParameters(info, context, input, functionContext);
        var result = info.Method.Invoke(instance, parameters);

        if (result is Task task)
        {
            await task;
            var resultProp = task.GetType().GetProperty("Result");
            if (resultProp != null && resultProp.PropertyType != typeof(void))
                return resultProp.GetValue(task);
            return null;
        }

        return result;
    }

    private static object[] BuildParameters(
        DurableFunctionInfo info,
        object durableContext,
        object? input,
        FunctionContext functionContext)
    {
        var methodParams = info.Method.GetParameters();
        var values = new object[methodParams.Length];

        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];

            if (param.ParameterType.IsAssignableFrom(durableContext.GetType()))
            {
                // TaskOrchestrationContext or TaskActivityContext parameter
                values[i] = durableContext;
            }
            else if (param.ParameterType == typeof(FunctionContext))
            {
                values[i] = functionContext;
            }
            else if (param.HasDefaultValue)
            {
                values[i] = ConvertInput(input, param.ParameterType) ?? param.DefaultValue!;
            }
            else
            {
                values[i] = ConvertInput(input, param.ParameterType)
                    ?? CreateDefault(param)!;
            }
        }

        return values;
    }

    private static object? ConvertInput(object? input, Type targetType)
    {
        if (input is null) return null;
        if (targetType.IsAssignableFrom(input.GetType())) return input;

        var json = JsonSerializer.Serialize(input, SerializerOptions);
        return JsonSerializer.Deserialize(json, targetType, SerializerOptions);
    }

    private static object? CreateDefault(System.Reflection.ParameterInfo param)
    {
        var type = param.ParameterType;
        if (type.IsValueType) return Activator.CreateInstance(type);
        return null;
    }

    private static TResult? ConvertResult<TResult>(object? raw)
    {
        if (raw is null) return default;
        if (raw is TResult typed) return typed;

        var json = JsonSerializer.Serialize(raw, SerializerOptions);
        return JsonSerializer.Deserialize<TResult>(json, SerializerOptions);
    }
}