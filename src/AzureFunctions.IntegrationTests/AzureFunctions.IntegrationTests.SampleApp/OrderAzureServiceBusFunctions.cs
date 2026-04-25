using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.IntegrationTests.SampleApp;

/// <summary>
/// Sample Azure Service Bus triggered functions demonstrating in-process integration testing
/// with <c>AzureServiceBusIntegration</c>.
/// </summary>
public class OrderAzureServiceBusFunctions
{
    /// <summary>Tracks typed messages processed by the queue trigger during tests.</summary>
    public static readonly List<OrderCreatedEvent> ProcessedOrders = new();

    /// <summary>Tracks subjects received by the <c>integration-tests-sub</c> subscription.</summary>
    public static readonly List<string?> ProcessedIntegrationSubjects = new();

    /// <summary>Tracks subjects received by the <c>analytics-sub</c> subscription.</summary>
    public static readonly List<string?> ProcessedAnalyticsSubjects = new();

    /// <summary>Captures inputs received by <see cref="ProcessSessionOrder"/>.</summary>
    public static readonly List<OrderCreatedEvent> SessionProcessedOrders = new();

    /// <summary>Captures all messages received by <see cref="ProcessBatchOrders"/>.</summary>
    public static readonly List<ServiceBusReceivedMessage> BatchReceivedMessages = new();

    /// <summary>Captures inputs received by <see cref="ProcessAndForwardOrder"/>.</summary>
    public static readonly List<OrderCreatedEvent> ForwardedOrders = new();

    /// <summary>Captures inputs received by <see cref="ProcessOrderWithActions"/>.</summary>
    public static readonly List<OrderCreatedEvent> ActionProcessedOrders = new();

    /// <summary>
    /// Captures orders processed by <see cref="ProcessOrderFromEnvQueue"/>.
    /// Used to verify that Azure Service Bus queue names resolved from environment variables
    /// (<c>%TestQueueName%</c> syntax) are correctly discovered and dispatched to.
    /// </summary>
    public static readonly List<OrderCreatedEvent> EnvQueueProcessedOrders = new();

    // ── Queue trigger ────────────────────────────────────────────────────────

    /// <summary>
    /// Processes an <see cref="OrderCreatedEvent"/> message from the <c>orders</c> queue.
    /// The trigger parameter is a custom typed model; the runtime deserializes the message body.
    /// </summary>
    [Function("ProcessOrderCreated")]
    public Task ProcessOrderCreated(
        [ServiceBusTrigger("orders", Connection = "ServiceBusConnection")] OrderCreatedEvent order,
        FunctionContext context)
    {
        ProcessedOrders.Add(order);
        return Task.CompletedTask;
    }

    // ── Topic triggers ───────────────────────────────────────────────────────

    /// <summary>
    /// Processes messages arriving on the <c>events</c> topic, <c>integration-tests-sub</c> subscription.
    /// </summary>
    [Function("ProcessEventIntegration")]
    public Task ProcessEventIntegration(
        [ServiceBusTrigger("events", "integration-tests-sub", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext context)
    {
        ProcessedIntegrationSubjects.Add(message.Subject);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes messages arriving on the <c>events</c> topic, <c>analytics-sub</c> subscription.
    /// </summary>
    [Function("ProcessEventAnalytics")]
    public Task ProcessEventAnalytics(
        [ServiceBusTrigger("events", "analytics-sub", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        FunctionContext context)
    {
        ProcessedAnalyticsSubjects.Add(message.Subject);
        return Task.CompletedTask;
    }

    // ── Session-enabled queue ─────────────────────────────────────────────────

    /// <summary>
    /// Processes orders from a session-enabled queue. Uses both
    /// <see cref="ServiceBusMessageActions"/> for message settlement and
    /// <see cref="ServiceBusSessionMessageActions"/> for session state management.
    /// Demonstrates Azure Service Bus session support in in-process integration tests.
    /// </summary>
    [Function("ProcessSessionOrder")]
    public async Task ProcessSessionOrder(
        [ServiceBusTrigger("session-orders", Connection = "ServiceBusConnection", IsSessionsEnabled = true)] OrderCreatedEvent order,
        ServiceBusMessageActions messageActions,
        ServiceBusSessionMessageActions sessionActions,
        FunctionContext context)
    {
        SessionProcessedOrders.Add(order);

        // Store session state
        var state = BinaryData.FromString(order.OrderId.ToString());
        await sessionActions.SetSessionStateAsync(state);

        await messageActions.CompleteMessageAsync(
            ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: order.OrderId.ToString()));
    }

    // ── Batched queue ─────────────────────────────────────────────────────────

    /// <summary>
    /// Processes a batch of raw <see cref="ServiceBusReceivedMessage"/> Azure Service Bus messages.
    /// The trigger parameter is <c>IReadOnlyList&lt;ServiceBusReceivedMessage&gt;</c>.
    /// </summary>
    [Function("ProcessBatchOrders")]
    public async Task ProcessBatchOrders(
        [ServiceBusTrigger("batch-orders", Connection = "ServiceBusConnection", IsBatched = true)] IReadOnlyList<ServiceBusReceivedMessage> messages,
        ServiceBusMessageActions messageActions,
        FunctionContext context)
    {
        foreach (var msg in messages)
        {
            BatchReceivedMessages.Add(msg);
            await messageActions.CompleteMessageAsync(msg);
        }
    }

    // ── Return value (output binding) ─────────────────────────────────────────

    /// <summary>
    /// Processes an order and returns a forwarded event as an output binding.
    /// The return value is sent to the <c>forwarded-orders</c> queue via the output binding.
    /// </summary>
    [Function("ProcessAndForwardOrder")]
    public Task<OrderForwardedEvent> ProcessAndForwardOrder(
        [ServiceBusTrigger("forward-orders", Connection = "ServiceBusConnection", AutoCompleteMessages = true)] OrderCreatedEvent order,
        FunctionContext context)
    {
        ForwardedOrders.Add(order);
        var forwarded = new OrderForwardedEvent
        {
            OriginalOrderId = order.OrderId,
            CustomerName = order.CustomerName,
            ForwardedAt = DateTime.UtcNow
        };
        return Task.FromResult(forwarded);
    }

    // ── Manual settlement via Azure Service Bus MessageActions ────────────────────────

    /// <summary>
    /// Processes an order and either completes or dead-letters based on business logic.
    /// Demonstrates <c>AutoCompleteMessages = false</c> style manual settlement.
    /// </summary>
    [Function("ProcessOrderWithActions")]
    public async Task ProcessOrderWithActions(
        [ServiceBusTrigger("orders-manual", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage rawMessage,
        ServiceBusMessageActions messageActions,
        FunctionContext context)
    {
        var order = System.Text.Json.JsonSerializer.Deserialize<OrderCreatedEvent>(rawMessage.Body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (order is null || order.TotalAmount <= 0)
        {
            await messageActions.DeadLetterMessageAsync(rawMessage,
                propertiesToModify: null,
                deadLetterReason: "InvalidOrder",
                deadLetterErrorDescription: "TotalAmount must be positive");
            return;
        }

        ActionProcessedOrders.Add(order);
        await messageActions.CompleteMessageAsync(rawMessage);
    }

    // ── Environment-variable queue name ───────────────────────────────────────

    /// <summary>
    /// Processes an <see cref="OrderCreatedEvent"/> from a queue whose name is supplied via
    /// the <c>TestQueueName</c> environment variable using the Azure Functions
    /// <c>%VariableName%</c> app-setting syntax.
    /// This function is used to verify that <c>FunctionAppFactory</c> correctly resolves
    /// environment variable references when discovering Azure Service Bus triggered functions.
    /// </summary>
    [Function("ProcessOrderFromEnvQueue")]
    public Task ProcessOrderFromEnvQueue(
        [ServiceBusTrigger("%TestQueueName%", Connection = "ServiceBusConnection")] OrderCreatedEvent order,
        FunctionContext context)
    {
        EnvQueueProcessedOrders.Add(order);
        return Task.CompletedTask;
    }
}

/// <summary>Payload model for an order-created Azure Service Bus message.</summary>
public class OrderCreatedEvent
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

/// <summary>Forwarded order event written to the output binding queue.</summary>
public class OrderForwardedEvent
{
    public int OriginalOrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime ForwardedAt { get; set; }
}