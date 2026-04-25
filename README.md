# AzureFunctions.IntegrationTests
[![NuGet](https://img.shields.io/nuget/v/AzureFunctions.IntegrationTests.svg)](https://www.nuget.org/packages/AzureFunctions.IntegrationTests/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A testing library for Azure Functions v4 (isolated worker process model) that provides a `FunctionAppFactory` similar to `WebApplicationFactory` in ASP.NET Core. This makes it easy to write integration tests for your Azure Functions without needing to start the actual Functions runtime.

## Features

- 🚀 **Easy to use** - Similar API to `WebApplicationFactory<T>` that developers already know
- 🔍 **Auto-discovery** - Automatically finds and invokes your Azure Functions
- 🧪 **In-memory testing** - No need to start the Functions host or use HTTP
- 🎯 **Full dependency injection** - Access to the service provider for testing
- 📝 **Route parameter support** - Handles complex routes with parameters like `{id}`, `{email}`, etc.
- ⚙️ **Customizable** - Virtual methods to override host configuration
- 🔄 **HttpClient integration** - Use familiar HttpClient for testing
- 📨 **Azure Service Bus testing** - Execute queue/topic-triggered functions without a real Service Bus namespace

## Installation

```bash
dotnet add package AzureFunctions.IntegrationTests
```

## Quick Start

### 0.  Modify Your Program.cs for Testability
First, expose the host creation logic in your Azure Functions project, add `public partial class Program` and expose the `GetHost` function
```csharp
var host= GetHost(args);

host.Run();

public partial class Program { 

    public static IHost GetHost(string[] args)
    {
         var builder = FunctionsApplication.CreateBuilder(args);
        builder.ConfigureFunctionsWebApplication();
        builder.Services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
        return builder.Build();
    }

}
```

### 1. Basic Usage

```csharp
using AzureFunctions.IntegrationTests;
using Xunit;

public class MyFunctionTests : IClassFixture<FunctionAppFactory<Program>>
{
    private readonly HttpClient _client;

    public MyFunctionTests(FunctionAppFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUser_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/users/123");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### 2. Accessing Services

```csharp
public class MyFunctionTests : IClassFixture<FunctionAppFactory<Program>>
{
    private readonly FunctionAppFactory<Program> _factory;

    public MyFunctionTests(FunctionAppFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CanAccessServices()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var myService = scope.ServiceProvider.GetRequiredService<IMyService>();

        // Act & Assert
        Assert.NotNull(myService);
    }
}
```

### 3. Custom Headers

```csharp
[Fact]
public async Task PostUser_WithCustomHeaders_ReturnsCreated()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Tenant-Id", "test-tenant");

    var content = new StringContent(
        JsonSerializer.Serialize(new { name = "John" }),
        Encoding.UTF8,
        "application/json");

    // Act
    var response = await client.PostAsync("/api/users", content);

    // Assert
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

## Advanced Usage

### Custom Factory for Test Configuration

You can create a custom factory to override services or configuration:

```csharp
public class CustomFunctionAppFactory : FunctionAppFactory<Program>
{
    protected override void ConfigureHostBuilder(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace a service with a mock
            services.AddScoped<IMyService, MockMyService>();

            // Add test-specific services
            services.AddSingleton<ITestDataSeeder, TestDataSeeder>();
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test-specific configuration
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:TestDb"] = "test-connection-string"
            });
        });
    }

    protected override void ConfigureEnvironment()
    {
        base.ConfigureEnvironment();

        // Set additional environment variables for testing
        Environment.SetEnvironmentVariable("TEST_MODE", "true");
    }
}

// Usage
public class MyTests : IClassFixture<CustomFunctionAppFactory>
{
    private readonly HttpClient _client;

    public MyTests(CustomFunctionAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Test_WithMockedServices()
    {
        // Your test implementation
    }
}
```

### Integration with xUnit Collection Fixtures

For sharing the factory across multiple test classes:

```csharp
[CollectionDefinition("Function Collection")]
public class FunctionCollection : ICollectionFixture<FunctionAppFactory<Program>>
{
    // This class has no code, it's just a marker for xUnit
}

[Collection("Function Collection")]
public class UserTests
{
    private readonly HttpClient _client;

    public UserTests(FunctionAppFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUser_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/users/123");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

[Collection("Function Collection")]
public class ProductTests
{
    private readonly HttpClient _client;

    public ProductTests(FunctionAppFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProduct_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/products/456");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### Testing Route Parameters

The library automatically handles route parameters:

```csharp
// Given an Azure Function with route: "users/{userId}/orders/{orderId}"
[Function("GetOrder")]
public IActionResult GetOrder(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{userId}/orders/{orderId}")]
    HttpRequestData req,
    Guid userId,
    int orderId)
{
    // Function implementation
}

// Test
[Fact]
public async Task GetOrder_WithRouteParameters_ReturnsOk()
{
    var response = await _client.GetAsync(
        "/api/users/550e8400-e29b-41d4-a716-446655440000/orders/123");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

## How It Works

1. **Auto-Discovery**: The factory scans your entry point assembly for Azure Functions (methods decorated with `[Function]` attribute)
2. **Host Creation**: It discovers your `GetHost()` or `CreateHostBuilder()` method and creates the host
3. **Request Routing**: When you make an HTTP request via the test client, it routes to the appropriate function
4. **Parameter Binding**: Route parameters are automatically extracted and converted to the correct types
5. **Response Conversion**: `IActionResult` responses are converted to `HttpResponseMessage` for easy assertion

## Requirements

- .NET 8.0 or later
- Azure Functions v4 (isolated worker process model)
- Your Azure Functions project must have a `Program` class with either:
  - `public static IHost GetHost(string[] args)` method, or
  - `public static IHostBuilder CreateHostBuilder(string[] args)` method

## Supported Features

- ✅ HTTP triggers (GET, POST, PUT, DELETE, PATCH, etc.)
- ✅ Route parameters (string, int, Guid, DateTime, enum, and nullable types)
- ✅ Query string parameters
- ✅ Request headers
- ✅ Request body
- ✅ IActionResult responses (OkObjectResult, NotFoundResult, BadRequestResult, etc.)
- ✅ Dependency injection
- ✅ Custom configuration
- ✅ Environment variables
- ✅ Azure Service Bus queue triggers (single message & batched)
- ✅ Azure Service Bus topic/subscription triggers
- ✅ Session-enabled queues (`IsSessionsEnabled = true`)
- ✅ Manual message settlement via `ServiceBusMessageActions` (Complete, DeadLetter, Abandon, Defer)
- ✅ Session state management via `ServiceBusSessionMessageActions`
- ✅ Return values / output bindings from Service Bus triggered functions

## Azure Service Bus Testing

Use `CreateAzureServiceBusFunctionExecutor()` to execute Azure Service Bus triggered functions
directly in-process without connecting to a real Azure Service Bus namespace.

### Queue execution

```csharp
public class OrderFunctionTests : IClassFixture<FunctionAppFactory<Program>>
{
    private readonly FunctionAppFactory<Program> _factory;

    public OrderFunctionTests(FunctionAppFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ProcessOrder_QueueTrigger_StoresOrder()
    {
        var executor = _factory.CreateAzureServiceBusFunctionExecutor();

        await executor.ExecuteQueueAsync("orders", new OrderCreatedEvent { OrderId = 1 });

        Assert.Single(OrderFunctions.ProcessedOrders);
    }
}
```

### Topic / subscription execution

```csharp
// Execute ALL subscriptions registered for the topic
await executor.ExecuteTopicAsync("events", new { EventId = 1 }, messageType: "OrderShipped");

// Execute only a specific subscription
await executor.ExecuteTopicAsync("events", new { EventId = 2 },
    subscriptionName: "analytics-sub", messageType: "OrderUpdated");
```

### Batched queue

```csharp
var orders = new[] { new OrderCreatedEvent { OrderId = 1 }, new OrderCreatedEvent { OrderId = 2 } };
var result = await executor.ExecuteBatchQueueAsync("batch-orders", orders);

Assert.Equal(2, result.MessageActions.CompletedMessages.Count);
```

### Session-enabled queue

```csharp
var result = await executor.ExecuteQueueAsync("session-orders", order);

Assert.NotNull(result.SessionMessageActions);
Assert.Equal("42", result.SessionMessageActions.SessionState!.ToString());
Assert.Single(result.MessageActions.CompletedMessages);
```

### Asserting message settlement

The `AzureServiceBusExecutionResult` returned by every execution method exposes:

| Property | Description |
|---|---|
| `ReturnValue` | Function's return value (output binding) |
| `MessageActions` | Spy recording Complete / DeadLetter / Abandon / Defer calls |
| `SessionMessageActions` | Spy recording session-state and session-lock calls (`null` for non-session functions) |

```csharp
// Check dead-letter
var result = await executor.ExecuteQueueAsync("orders-manual", invalidOrder);
Assert.Single(result.MessageActions.DeadLetteredMessages);
var (_, reason, description, _) = result.MessageActions.DeadLetteredMessages[0];
Assert.Equal("InvalidOrder", reason);

// Check return value (output binding)
var result = await executor.ExecuteQueueAsync("forward-orders", order);
var forwarded = Assert.IsType<OrderForwardedEvent>(result.ReturnValue);
Assert.Equal(order.OrderId, forwarded.OriginalOrderId);
```

## Limitations

- Does not test the actual HTTP binding (e.g., authentication middleware at the HTTP level)
- Timer, Blob, Event Hub, and other non-HTTP / non-Service-Bus trigger types are not yet supported
- `ExecuteBatchTopicAsync` requires every matching subscription function to be configured with `IsBatched = true`

## Example Project Structure

```
MyAzureFunctionsApp/
├── src/
│   ├── MyApp.API/                    # Your Azure Functions project
│   │   ├── Functions/
│   │   │   ├── UserFunctions.cs
│   │   │   └── ProductFunctions.cs
│   │   └── Program.cs
│   └── MyApp.Core/                   # Your business logic
│       └── Services/
│           └── UserService.cs
└── tests/
    └── MyApp.IntegrationTests/
        ├── UserFunctionsTests.cs
        └── ProductFunctionsTests.cs
```


## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

Inspired by ASP.NET Core's `WebApplicationFactory<TEntryPoint>` pattern for testing.
