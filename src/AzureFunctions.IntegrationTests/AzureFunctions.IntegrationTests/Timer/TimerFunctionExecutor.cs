using AzureFunctions.IntegrationTests.Mocks;
using AzureFunctions.IntegrationTests.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace AzureFunctions.IntegrationTests.Timer;

/// <summary>
/// Dispatches calls to Azure Functions with a <see cref="TimerTriggerAttribute"/> binding,
/// enabling in-process integration testing without a live timer scheduler.
/// Obtain an instance via <c>FunctionAppFactory.CreateTimerFunctionExecutor()</c>.
/// </summary>
internal sealed class TimerFunctionExecutor : ITimerFunctionExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, TimerFunctionInfo> _functions;

    internal TimerFunctionExecutor(
        IServiceProvider serviceProvider,
        IEnumerable<TimerFunctionInfo> functions)
    {
        _serviceProvider = serviceProvider;
        _functions = new Dictionary<string, TimerFunctionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in functions)
            _functions[info.FunctionName] = info;
    }

    /// <summary>
    /// Invokes the timer-triggered function identified by <paramref name="functionName"/> as
    /// if it were fired on schedule (not past-due).
    /// </summary>
    /// <param name="functionName">The name declared in <c>[Function("…")]</c>.</param>
    /// <param name="isPastDue">
    /// When <see langword="true"/> the supplied <see cref="TimerInfo"/> reports
    /// <see cref="TimerInfo.IsPastDue"/> as <see langword="true"/>.
    /// Useful for testing past-due guard logic.
    /// </param>
    /// <returns>A <see cref="TimerFunctionExecutionResult"/> with the captured return value (if any).</returns>
    public Task<TimerFunctionExecutionResult> FireAsync(string functionName, bool isPastDue = false)
    {
        if (!_functions.TryGetValue(functionName, out var info))
        {
            throw new InvalidOperationException(
                $"No timer-triggered function found with name '{functionName}'. " +
                $"Available functions: {string.Join(", ", _functions.Keys)}");
        }

        return InvokeAsync(info, isPastDue);
    }

    private async Task<TimerFunctionExecutionResult> InvokeAsync(TimerFunctionInfo info, bool isPastDue)
    {
        using var scope = _serviceProvider.CreateScope();

        var instance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, info.FunctionType);
        var context = new SimpleFunctionContext(scope.ServiceProvider);
        var timerInfo = BuildTimerInfo(isPastDue);

        var parameters = BuildParameters(info, timerInfo, context);

        var result = info.Method.Invoke(instance, parameters);

        if (result is Task task)
            await task;

        return new TimerFunctionExecutionResult { TimerInfo = timerInfo };
    }

    private static TimerInfo BuildTimerInfo(bool isPastDue)
    {
        return new TimerInfo
        {
            IsPastDue = isPastDue,
            ScheduleStatus = new ScheduleStatus
            {
                Last = DateTimeOffset.UtcNow.AddMinutes(-5).UtcDateTime,
                Next = DateTimeOffset.UtcNow.AddMinutes(5).UtcDateTime,
                LastUpdated = DateTimeOffset.UtcNow.UtcDateTime,
            },
        };
    }

    private static object[] BuildParameters(
        TimerFunctionInfo info,
        TimerInfo timerInfo,
        FunctionContext context)
    {
        var methodParams = info.Method.GetParameters();
        var values = new object[methodParams.Length];

        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];

            if (param.ParameterType == typeof(TimerInfo))
                values[i] = timerInfo;
            else if (param.ParameterType == typeof(FunctionContext))
                values[i] = context;
            else if (param.HasDefaultValue)
                values[i] = param.DefaultValue!;
            else
            {
                var created = Activator.CreateInstance(param.ParameterType);
                values[i] = created
                    ?? throw new InvalidOperationException(
                        $"Cannot create instance of parameter type '{param.ParameterType.FullName}'.");
            }
        }

        return values;
    }
}