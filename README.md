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
- 📨 **Azure Service Bus integration** - Execute queue/topic-triggered functions without a real Azure Service Bus namespace

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
- ✅ Return values / output bindings from Azure Service Bus triggered functions
- ✅ Direct `ServiceBusReceivedMessage` pass-through (full metadata control)
- ✅ Environment-variable queue/topic name resolution (`%VariableName%` syntax)
- ✅ Non-generic execute overloads (no type argument needed when return value is irrelevant)
- ✅ Durable Functions — activity and orchestrator execution in-process (`IDurableFunctionExecutor`)
- ✅ Timer triggers — fire on schedule or past-due in-process (`ITimerFunctionExecutor`)

## Azure Service Bus Testing

Use `CreateAzureServiceBusFunctionExecutor()` to obtain an `IAzureServiceBusFunctionExecutor`
and execute Azure Service Bus triggered functions directly in-process without connecting to
a real Azure Service Bus namespace.

### Non-generic overloads (no return value)

When the function's output binding is not relevant to the test, omit the type argument entirely.
The four non-generic overloads return `AzureServiceBusExecutionResult<object>`, so
`MessageActions` and `SessionMessageActions` are still accessible for assertion.

```csharp
// Queue — no type argument
var result = await executor.ExecuteQueueAsync("orders", message);
Assert.Single(result.MessageActions.CompletedMessages);

// Batched queue — no type argument
var result = await executor.ExecuteBatchQueueAsync("batch-orders", messages);
Assert.Equal(2, result.MessageActions.CompletedMessages.Count);

// Topic (all subscriptions) — no type argument
await executor.ExecuteTopicAsync("events", message);

// Topic (specific subscription) — no type argument
var result = await executor.ExecuteTopicAsync("events", message,
    subscriptionName: "analytics-sub");
Assert.NotNull(result.MessageActions);
```

When you **do** care about the output binding, pass the expected type as a generic argument
to get a strongly-typed `ReturnValue` without any cast:

```csharp
// Strongly-typed return value — no cast needed
var result = await executor.ExecuteQueueAsync<OrderForwardedEvent>("forward-orders", message);
Assert.Equal(42, result.ReturnValue!.OriginalOrderId);
```

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

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 1 }),
            messageId: "msg-001");

        // No type argument needed when the return value is not relevant
        await executor.ExecuteQueueAsync("orders", message);

        Assert.Single(OrderFunctions.ProcessedOrders);
    }
}
```

### Topic / subscription execution

```csharp
// Execute ALL subscriptions registered for the topic
await executor.ExecuteTopicAsync("events",
    ServiceBusModelFactory.ServiceBusReceivedMessage(
        body: BinaryData.FromObjectAsJson(new { EventId = 1 }),
        subject: "OrderShipped"));

// Execute only a specific subscription
await executor.ExecuteTopicAsync("events",
    ServiceBusModelFactory.ServiceBusReceivedMessage(
        body: BinaryData.FromObjectAsJson(new { EventId = 2 }),
        subject: "OrderUpdated"),
    subscriptionName: "analytics-sub");
```

### Batched queue

```csharp
var messages = new[]
{
    ServiceBusModelFactory.ServiceBusReceivedMessage(
        body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 1 }),
        messageId: "msg-001"),
    ServiceBusModelFactory.ServiceBusReceivedMessage(
        body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 2 }),
        messageId: "msg-002"),
};

var result = await executor.ExecuteBatchQueueAsync("batch-orders", messages);

Assert.Equal(2, result.MessageActions.CompletedMessages.Count);
```

### Session-enabled queue

```csharp
var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
    body: BinaryData.FromObjectAsJson(order),
    messageId: "msg-session-001");

var result = await executor.ExecuteQueueAsync("session-orders", message);

Assert.NotNull(result.SessionMessageActions);
Assert.Equal("42", result.SessionMessageActions.SessionState!.ToString());
Assert.Single(result.MessageActions.CompletedMessages);
```

### Asserting message settlement

The `AzureServiceBusExecutionResult<T>` returned by every execution method exposes:

| Property | Description |
|---|---|
| `ReturnValue` | Strongly-typed return value of the function (output binding); `null` for `void`/`Task` functions |
| `MessageActions` | Spy recording Complete / DeadLetter / Abandon / Defer calls |
| `SessionMessageActions` | Spy recording session-state and session-lock calls (`null` for non-session functions) |

Specify the function's output-binding type as the generic argument to get a strongly-typed
`ReturnValue` without casting.

```csharp
// Check dead-letter — omit the type argument when the return value is not relevant
var result = await executor.ExecuteQueueAsync("orders-manual", invalidMessage);
Assert.Single(result.MessageActions.DeadLetteredMessages);
var (_, reason, description, _) = result.MessageActions.DeadLetteredMessages[0];
Assert.Equal("InvalidOrder", reason);

// Check return value (output binding) — specify the expected type; no cast required
var result = await executor.ExecuteQueueAsync<OrderForwardedEvent>("forward-orders", message);
Assert.NotNull(result.ReturnValue);               // ReturnValue is already OrderForwardedEvent
Assert.Equal(order.OrderId, result.ReturnValue.OriginalOrderId);
```

### Direct `ServiceBusReceivedMessage` pass-through

All four execute methods accept a pre-built `ServiceBusReceivedMessage` directly,
skipping serialisation entirely. Use this when you need full control over message metadata —
subject, message-id, correlation-id, application properties, or a hand-crafted body.

```csharp
// Build the message with full metadata control
var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
    body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 1 }),
    subject: "order.created",
    messageId: "msg-001",
    correlationId: "trace-abc",
    properties: new Dictionary<string, object> { ["tenantId"] = "tenant-xyz" });

// Single queue message — omit the type argument when the return value is not relevant
var result = await executor.ExecuteQueueAsync("orders", message);

// Single topic message (specific subscription)
await executor.ExecuteTopicAsync("events", message, subscriptionName: "analytics-sub");

// Batched queue
var batch = new[] { message1, message2 };
var result = await executor.ExecuteBatchQueueAsync("batch-orders", batch);

// Batched topic
await executor.ExecuteBatchTopicAsync("events", batch, subscriptionName: "integration-tests-sub");
```

### Environment-variable queue/topic names

Azure Functions supports `%VariableName%` syntax in trigger attribute properties.
`FunctionAppFactory` resolves these against `Environment.GetEnvironmentVariable` at startup time.

```csharp
// Function definition
[Function("ProcessOrderFromEnvQueue")]
public Task ProcessOrderFromEnvQueue(
    [ServiceBusTrigger("%TestQueueName%", Connection = "ServiceBusConnection")] OrderCreatedEvent order,
    FunctionContext context) { ... }

// Test — set the env var BEFORE constructing the factory
Environment.SetEnvironmentVariable("TestQueueName", "my-test-queue");
using var factory = new FunctionAppFactory<Program>();
var executor = factory.CreateAzureServiceBusFunctionExecutor();

var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
    body: BinaryData.FromObjectAsJson(order));

await executor.ExecuteQueueAsync("my-test-queue", message);
```

## Limitations

- Does not test the actual HTTP binding (e.g., authentication middleware at the HTTP level)
- Timer, Blob, Event Hub, and other non-HTTP / non-Azure-Service-Bus / non-Durable trigger types are not yet supported
- `ExecuteBatchTopicAsync` requires every matching subscription function to be configured with `IsBatched = true`
- Durable `WaitForExternalEvent` and `CallSubOrchestratorAsync` are not supported in mock context

## Durable Functions Testing

Use `CreateDurableFunctionExecutor()` to execute Durable Functions orchestrators and activities
in-process without a live Durable Task hub.

### Activity execution

```csharp
public class OrderDurableTests : IClassFixture<FunctionAppFactory<Program>>
{
    private readonly FunctionAppFactory<Program> _factory;

    public OrderDurableTests(FunctionAppFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ProcessOrderActivity_ValidOrder_ReturnsTrue()
    {
        var executor = _factory.CreateDurableFunctionExecutor();
        var order = new OrderPayload(OrderId: 1, CustomerEmail: "alice@example.com", Amount: 99.99m);

        // Typed input + typed output
        var result = await executor.ExecuteActivityAsync<OrderPayload, bool>(
            "ProcessOrderActivity", order);

        Assert.True(result);
    }

    [Fact]
    public async Task SendConfirmationActivity_RecordsEmail()
    {
        var executor = _factory.CreateDurableFunctionExecutor();

        // void activity — fire-and-forget
        await executor.ExecuteActivityAsync<string>("SendConfirmationActivity", "alice@example.com");

        Assert.Single(OrderDurableFunctions.SentEmails);
    }
}
```

### Orchestrator execution

Use `MockTaskOrchestrationContext` to configure what each activity call should return, then pass
it to `ExecuteOrchestratorAsync`. Chain `MockActivity` calls fluently before invoking the orchestrator.

```csharp
[Fact]
public async Task ProcessOrderOrchestrator_ValidOrder_ReturnsTrueAndSendsEmail()
{
    var executor = _factory.CreateDurableFunctionExecutor();
    var order = new OrderPayload(OrderId: 42, CustomerEmail: "dave@example.com", Amount: 150m);

    var context = new MockTaskOrchestrationContext(input: order)
        // Mock a typed-return activity
        .MockActivity<OrderPayload, bool>("ProcessOrderActivity", _ => true)
        // Mock a void activity using the Action overload
        .MockActivity<string>("SendConfirmationActivity", _ => { });

    var result = await executor.ExecuteOrchestratorAsync<bool>(
        "ProcessOrderOrchestrator", context);

    Assert.True(result);
}
```

### MockActivity overloads

| Overload | Use case |
|---|---|
| `MockActivity<TInput, TResult>(name, Func<TInput?, TResult>)` | Activity with typed input and return value |
| `MockActivity<TResult>(name, TResult)` | Activity that always returns a fixed value |
| `MockActivity<TInput>(name, Action<TInput?>)` | Void / fire-and-forget activity (no return value) |
| `MockActivity<TInput, TResult>(name, Func<TInput?, Task<TResult>>)` | Async activity handler |

### Asserting orchestrator state

```csharp
// CustomStatus set via context.SetCustomStatus(...)
Assert.Equal("processing", context.CustomStatus);

// Unmocked activity throws with a descriptive message
await Assert.ThrowsAsync<InvalidOperationException>(
    () => executor.ExecuteOrchestratorAsync<bool>("MyOrchestrator", emptyContext));
```

### Untyped input (JSON round-trip)

```csharp
// The executor JSON-round-trips anonymous objects to the activity's parameter type
var raw = new { orderId = 5, customerEmail = "carol@example.com", amount = 50.0m };
var result = await executor.ExecuteActivityAsync<bool>("ProcessOrderActivity", raw);
```

## Timer Function Testing

Use `CreateTimerFunctionExecutor()` to fire timer-triggered functions in-process without a
live timer scheduler.

```csharp
public class CleanupTimerTests : IClassFixture<FunctionAppFactory<Program>>
{
    private readonly FunctionAppFactory<Program> _factory;

    public CleanupTimerTests(FunctionAppFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task DailyCleanup_OnSchedule_Executes()
    {
        var executor = _factory.CreateTimerFunctionExecutor();

        var result = await executor.FireAsync("DailyCleanup");

        Assert.Equal(1, CleanupTimerFunctions.CleanupRunCount);
        Assert.False(result.TimerInfo.IsPastDue);
    }

    [Fact]
    public async Task DailyCleanup_WhenPastDue_FunctionReceivesPastDueTrue()
    {
        var executor = _factory.CreateTimerFunctionExecutor();

        var result = await executor.FireAsync("DailyCleanup", isPastDue: true);

        Assert.True(result.TimerInfo.IsPastDue);
        Assert.True(CleanupTimerFunctions.LastRunWasPastDue);
    }
}
```

### TimerFunctionExecutionResult

| Property | Description |
|---|---|
| `TimerInfo` | The `TimerInfo` instance passed to the function, including `IsPastDue` and `ScheduleStatus` |

```csharp
var result = await executor.FireAsync("DailyCleanup");
Assert.NotNull(result.TimerInfo.ScheduleStatus);
Assert.True(result.TimerInfo.ScheduleStatus.Next > result.TimerInfo.ScheduleStatus.Last);
```

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
