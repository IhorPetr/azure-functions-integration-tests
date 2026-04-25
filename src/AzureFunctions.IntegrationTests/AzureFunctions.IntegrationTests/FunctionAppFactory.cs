using System.Reflection;
using Azure.Messaging.ServiceBus;
using AzureFunctions.IntegrationTests.AzureServiceBus;
using AzureFunctions.IntegrationTests.Extensions;
using AzureFunctions.IntegrationTests.Http;
using AzureFunctions.IntegrationTests.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AzureFunctions.IntegrationTests;

/// <summary>
/// Factory for bootstrapping Azure Functions applications in-memory for functional end-to-end tests.
/// Similar to WebApplicationFactory for ASP.NET Core.
/// </summary>
/// <typeparam name="TEntryPoint">
/// A type in the entry point assembly of the Azure Functions app.
/// Typically the Program class. The assembly will be scanned to discover the host builder.
/// </typeparam>
public class FunctionAppFactory<TEntryPoint> : IDisposable where TEntryPoint : class
{
    private readonly IHost _host;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, List<FunctionInfo>> _functionRoutes;
    private readonly List<AzureServiceBusFunctionInfo> _serviceBusFunctions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of FunctionAppFactory
    /// </summary>
    public FunctionAppFactory()
    {
        // Configure default environment for testing
        ConfigureEnvironment();

        // Discover and build the host
        _host = CreateHost();

        // Discover all Azure Functions and their routes
        _functionRoutes = DiscoverFunctionRoutes();
        
        // Discover all Azure Service Bus-triggered functions
        _serviceBusFunctions = DiscoverAzureServiceBusFunctions();

        // Use the host's service provider
        _serviceProvider = _host.Services;

        // Start the host in the background
        _ = _host.StartAsync();
    }

    /// <summary>
    /// Gets the service provider from the hosted application
    /// </summary>
    public IServiceProvider Services => _serviceProvider;
    
    /// <summary>
    /// Creates an <see cref="IAzureServiceBusFunctionInvoker"/> that can invoke both queue-triggered and
    /// topic-triggered functions discovered in the entry-point assembly without a live
    /// Azure Service Bus. Use <see cref="IAzureServiceBusFunctionInvoker.InvokeQueueAsync"/> for
    /// queue triggers and <see cref="IAzureServiceBusFunctionInvoker.InvokeTopicAsync"/> for topic
    /// subscription triggers.
    /// </summary>
    public IAzureServiceBusFunctionInvoker CreateAzureServiceBusFunctionInvoker()
        => new AzureServiceBusFunctionInvoker(_serviceProvider, _serviceBusFunctions);

    /// <summary>
    /// Creates an HttpClient configured to make requests to the in-memory test server
    /// </summary>
    /// <returns>An HttpClient instance</returns>
    public HttpClient CreateClient()
    {
        return new TestHttpClient(this);
    }

    /// <summary>
    /// Creates an HttpClient configured to make requests to the in-memory test server
    /// with a custom base address
    /// </summary>
    /// <param name="baseAddress">The base address for the HttpClient</param>
    /// <returns>An HttpClient instance</returns>
    public HttpClient CreateClient(Uri baseAddress)
    {
        var client = new TestHttpClient(this);
        client.BaseAddress = baseAddress;
        return client;
    }

    /// <summary>
    /// Creates the host instance. Override this method to customize host creation.
    /// </summary>
    /// <returns>The configured IHost instance</returns>
    protected virtual IHost CreateHost()
    {
        var assembly = typeof(TEntryPoint).Assembly;

        // Try to find a GetHost method (static method that returns IHost)
        var getHostMethod = FindGetHostMethod(assembly);
        if (getHostMethod != null)
        {
            var host = getHostMethod.Invoke(null, new object[] { Array.Empty<string>() }) as IHost;
            if (host != null)
            {
                ConfigureHost(host);
                return host;
            }
        }

        // Try to find CreateHostBuilder method
        var createHostBuilderMethod = FindCreateHostBuilderMethod(assembly);
        if (createHostBuilderMethod != null)
        {
            var hostBuilder = createHostBuilderMethod.Invoke(null, new object[] { Array.Empty<string>() }) as IHostBuilder;
            if (hostBuilder != null)
            {
                ConfigureHostBuilder(hostBuilder);
                return hostBuilder.Build();
            }
        }

        throw new InvalidOperationException(
            $"Could not find a suitable method to create the host in assembly '{assembly.FullName}'. " +
            $"Ensure the entry point class has a 'public static IHost GetHost(string[] args)' or " +
            $"'public static IHostBuilder CreateHostBuilder(string[] args)' method.");
    }

    /// <summary>
    /// Override this method to configure the host after it's been created but before it's started.
    /// </summary>
    /// <param name="host">The host to configure</param>
    protected virtual void ConfigureHost(IHost host)
    {
        // Derived classes can override to customize the host
    }

    /// <summary>
    /// Override this method to configure the host builder before the host is built.
    /// </summary>
    /// <param name="builder">The host builder to configure</param>
    protected virtual void ConfigureHostBuilder(IHostBuilder builder)
    {
        // Derived classes can override to customize the host builder
    }

    /// <summary>
    /// Configures environment variables for testing. Override to customize.
    /// </summary>
    protected virtual void ConfigureEnvironment()
    {
        Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
    }

    /// <summary>
    /// Finds a static method named "GetHost" that returns IHost in the given assembly
    /// </summary>
    private static MethodInfo? FindGetHostMethod(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var method = type.GetMethod("GetHost",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string[]) },
                null);

            if (method != null && typeof(IHost).IsAssignableFrom(method.ReturnType))
            {
                return method;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a static method named "CreateHostBuilder" that returns IHostBuilder in the given assembly
    /// </summary>
    private static MethodInfo? FindCreateHostBuilderMethod(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var method = type.GetMethod("CreateHostBuilder",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string[]) },
                null);

            if (method != null && typeof(IHostBuilder).IsAssignableFrom(method.ReturnType))
            {
                return method;
            }
        }

        return null;
    }

    /// <summary>
    /// Discovers all Azure Functions in the entry point assembly
    /// </summary>
    private Dictionary<string, List<FunctionInfo>> DiscoverFunctionRoutes()
    {
        var routes = new Dictionary<string, List<FunctionInfo>>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(TEntryPoint).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                // Look for methods with FunctionAttribute
                var functionAttr = method.GetCustomAttribute<FunctionAttribute>();
                if (functionAttr == null) continue;

                // Look for HttpTrigger parameter to get the route
                foreach (var param in method.GetParameters())
                {
                    var httpTriggerAttr = param.GetCustomAttribute<HttpTriggerAttribute>();
                    if (httpTriggerAttr != null)
                    {
                        var route = httpTriggerAttr.Route ?? functionAttr.Name.ToLowerInvariant();
                        var methods = httpTriggerAttr.Methods ?? new[] { "GET", "POST", "PUT", "DELETE" };

                        var functionInfo = new FunctionInfo
                        {
                            FunctionType = type,
                            Method = method,
                            Route = route,
                            HttpMethods = methods,
                            FunctionName = functionAttr.Name
                        };

                        // Support multiple functions per route (different HTTP methods)
                        if (!routes.ContainsKey(route))
                        {
                            routes[route] = new List<FunctionInfo>();
                        }
                        routes[route].Add(functionInfo);

                        break;
                    }
                }
            }
        }

        return routes;
    }
    
        /// <summary>
    /// Discovers all Azure Functions with an Azure Service Bus trigger binding in the entry-point assembly
    /// </summary>
    private List<AzureServiceBusFunctionInfo> DiscoverAzureServiceBusFunctions()
    {
        var result = new List<AzureServiceBusFunctionInfo>();
        var assembly = typeof(TEntryPoint).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var functionAttr = method.GetCustomAttribute<FunctionAttribute>();
                if (functionAttr == null) continue;

                foreach (var param in method.GetParameters())
                {
                    var triggerAttr = param.GetCustomAttribute<ServiceBusTriggerAttribute>();
                    if (triggerAttr == null) continue;

                    var entityPath = !string.IsNullOrEmpty(triggerAttr.TopicName)
                        ? ResolveServiceBusEntityPath(triggerAttr.TopicName)
                        : ResolveServiceBusEntityPath(triggerAttr.QueueName ?? string.Empty);

                    // IsBatched: detected from the trigger parameter type being a collection
                    var isBatched =
                        param.ParameterType == typeof(IReadOnlyList<ServiceBusReceivedMessage>)
                        || param.ParameterType == typeof(ServiceBusReceivedMessage[]);

                    // IsSessionsEnabled: detected from the method having a session actions parameter
                    var isSessionsEnabled = method.GetParameters()
                        .Any(p => p.ParameterType == typeof(ServiceBusSessionMessageActions));

                    result.Add(new AzureServiceBusFunctionInfo
                    {
                        FunctionType = type,
                        Method = method,
                        EntityPath = entityPath,
                        SubscriptionName = triggerAttr.SubscriptionName,
                        FunctionName = functionAttr.Name,
                        TriggerParameter = param,
                        IsTopicTrigger = !string.IsNullOrEmpty(triggerAttr.TopicName),
                        IsBatched = isBatched,
                        IsSessionsEnabled = isSessionsEnabled,
                    });

                    break; // only one trigger per function
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves an Azure Service Bus entity path that may use the <c>%VariableName%</c>
    /// app-setting / environment-variable syntax supported by the Azure Functions runtime.
    /// If the path is wrapped in <c>%…%</c> the matching environment variable value is returned;
    /// if the variable is not set the raw placeholder is returned unchanged.
    /// </summary>
    /// <param name="path">The raw queue name, topic name, or <c>%EnvVarName%</c> token.</param>
    /// <returns>The resolved entity path.</returns>
    private static string ResolveServiceBusEntityPath(string path)
    {
        if (path.Length > 2 && path[0] == '%' && path[^1] == '%')
        {
            var variableName = path[1..^1];
            return Environment.GetEnvironmentVariable(variableName) ?? path;
        }

        return path;
    }

    /// <summary>
    /// Executes a function with the given route and HTTP method
    /// </summary>
    internal async Task<TestHttpResponse> ExecuteFunctionAsync(
        string route,
        string httpMethod,
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? queryString = null)
    {
        // Find matching route
        var functionInfo = FindMatchingRoute(route, httpMethod);
        if (functionInfo == null)
        {
            return new TestHttpResponse
            {
                StatusCode = System.Net.HttpStatusCode.NotFound,
                Content = System.Text.Json.JsonSerializer.Serialize(new { error = $"No function found for route: {route}" })
            };
        }

        // Check if HTTP method is supported
        if (!Array.Exists(functionInfo.HttpMethods, m => m.Equals(httpMethod, StringComparison.OrdinalIgnoreCase)))
        {
            return new TestHttpResponse
            {
                StatusCode = System.Net.HttpStatusCode.MethodNotAllowed,
                Content = System.Text.Json.JsonSerializer.Serialize(new { error = $"Method {httpMethod} not allowed for route: {route}" })
            };
        }

        using var scope = Services.CreateScope();

        // Create function instance
        var functionInstance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, functionInfo.FunctionType);

        // Create mock request
        var mockRequest = new Mocks.SimpleHttpRequestData(headers, body, route, queryString);

        try
        {
            // Prepare parameters for method invocation
            var methodParams = PrepareMethodParameters(functionInfo, route, mockRequest, scope.ServiceProvider);

            // Invoke the function method
            var methodResult = functionInfo.Method.Invoke(functionInstance, methodParams);

            Microsoft.AspNetCore.Mvc.IActionResult? actionResult;

            // Handle async results
            if (methodResult is Task task)
            {
                await task;
                var resultProperty = task.GetType().GetProperty("Result");
                actionResult = resultProperty?.GetValue(task) as Microsoft.AspNetCore.Mvc.IActionResult;
            }
            else
            {
                actionResult = methodResult as Microsoft.AspNetCore.Mvc.IActionResult;
            }

            if (actionResult == null)
            {
                return new TestHttpResponse
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Content = System.Text.Json.JsonSerializer.Serialize(new { error = "Function did not return IActionResult" })
                };
            }

            return ActionResultConverter.ConvertToTestHttpResponse(actionResult);
        }
        catch (Exception ex)
        {
            return new TestHttpResponse
            {
                StatusCode = System.Net.HttpStatusCode.InternalServerError,
                Content = System.Text.Json.JsonSerializer.Serialize(new { error = ex.InnerException?.Message ?? ex.Message })
            };
        }
    }

    private FunctionInfo? FindMatchingRoute(string requestPath, string httpMethod)
    {
        // Try exact match first
        if (_functionRoutes.TryGetValue(requestPath, out var exactMatchFunctions))
        {
            var methodMatch = exactMatchFunctions.FirstOrDefault(f =>
                Array.Exists(f.HttpMethods, m => m.Equals(httpMethod, StringComparison.OrdinalIgnoreCase)));

            return methodMatch ?? exactMatchFunctions.First();
        }

        // Try pattern matching for routes with parameters
        foreach (var kvp in _functionRoutes)
        {
            var routePattern = kvp.Key;
            if (routePattern.Contains('{'))
            {
                if (IsRouteMatch(routePattern, requestPath))
                {
                    var methodMatch = kvp.Value.FirstOrDefault(f =>
                        Array.Exists(f.HttpMethods, m => m.Equals(httpMethod, StringComparison.OrdinalIgnoreCase)));

                    return methodMatch ?? kvp.Value.First();
                }
            }
        }

        return null;
    }

    private static bool IsRouteMatch(string routePattern, string requestPath)
    {
        var patternParts = routePattern.Split('/');
        var requestParts = requestPath.Split('/');

        if (patternParts.Length != requestParts.Length) return false;

        for (int i = 0; i < patternParts.Length; i++)
        {
            if (!patternParts[i].StartsWith('{') &&
                !patternParts[i].Equals(requestParts[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private object[] PrepareMethodParameters(
        FunctionInfo functionInfo,
        string route,
        Mocks.SimpleHttpRequestData mockRequest,
        IServiceProvider serviceProvider)
    {
        var methodParams = functionInfo.Method.GetParameters();
        var paramValues = new object[methodParams.Length];

        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];

            if (param.ParameterType == typeof(HttpRequestData) ||
                param.GetCustomAttribute<HttpTriggerAttribute>() != null)
            {
                paramValues[i] = mockRequest;
            }
            else if (param.ParameterType == typeof(FunctionContext))
            {
                paramValues[i] = new Mocks.SimpleFunctionContext(serviceProvider);
            }
            else
            {
                // Extract route parameters
                var routeParamValue = ExtractRouteParameter(functionInfo.Route, route, param.Name!);
                if (routeParamValue != null)
                {
                    paramValues[i] = RouteParameterConverter.ConvertParameter(routeParamValue, param.ParameterType);
                }
                else if (param.HasDefaultValue)
                {
                    paramValues[i] = param.DefaultValue!;
                }
                else
                {
                    paramValues[i] = Activator.CreateInstance(param.ParameterType)!;
                }
            }
        }

        return paramValues;
    }

    private static string? ExtractRouteParameter(string routeTemplate, string actualRoute, string paramName)
    {
        var routeParts = routeTemplate.Split('/');
        var actualParts = actualRoute.Split('/');

        if (routeParts.Length != actualParts.Length) return null;

        for (int i = 0; i < routeParts.Length; i++)
        {
            if (routeParts[i].StartsWith('{') && routeParts[i].EndsWith('}'))
            {
                var parameterName = routeParts[i].Trim('{', '}');
                if (parameterName.Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return actualParts[i];
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Disposes the factory and stops the host
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    _host?.StopAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore errors during shutdown
                }

                _host?.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Disposes the factory and stops the host
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
