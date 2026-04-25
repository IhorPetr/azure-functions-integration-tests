using Azure.Messaging.ServiceBus;
using AzureFunctions.IntegrationTests.AzureServiceBus;
using AzureFunctions.IntegrationTests.Mocks.AzureServiceBus;

namespace AzureFunctions.IntegrationTests.SampleApp.Tests;

/// <summary>
/// Integration tests for <see cref="AzureServiceBusFunctionExecutor"/>.
/// Covers queue execution, topic execution, session-enabled queues, batched queues,
/// return-value / output bindings, and manual message settlement (Complete / DeadLetter / Abandon / Defer).
/// Functions are invoked in-process without a real Azure Service Bus connection.
/// </summary>
public class AzureServiceBusIntegrationTests : IClassFixture<FunctionAppFactory<Program>>, IDisposable
{
    private readonly FunctionAppFactory<Program> _factory;

    public AzureServiceBusIntegrationTests(FunctionAppFactory<Program> factory)
    {
        _factory = factory;
        ResetState();
    }

    public void Dispose() => ResetState();

    private static void ResetState()
    {
        OrderAzureServiceBusFunctions.ProcessedOrders.Clear();
        OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects.Clear();
        OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects.Clear();
        OrderAzureServiceBusFunctions.EnvQueueProcessedOrders.Clear();
    }

    // ── Queue execution ───────────────────────────────────────────────────────

    [Fact]
    public void CreateServiceBusDispatcher_ShouldReturnDispatcher()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        Assert.NotNull(dispatcher);
    }

    [Fact]
    public async Task ExecuteQueueAsync_TypedMessage_ShouldInvokeTriggerFunction()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        var order = new OrderCreatedEvent { OrderId = 42, CustomerName = "Alice", TotalAmount = 99.99m };

        await dispatcher.ExecuteQueueAsync("orders", order);

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedOrders);
        Assert.Equal(42, OrderAzureServiceBusFunctions.ProcessedOrders[0].OrderId);
        Assert.Equal("Alice", OrderAzureServiceBusFunctions.ProcessedOrders[0].CustomerName);
        Assert.Equal(99.99m, OrderAzureServiceBusFunctions.ProcessedOrders[0].TotalAmount);
    }

    [Fact]
    public async Task ExecuteQueueAsync_WithMessageType_ShouldSetSubjectOnMessage()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        await dispatcher.ExecuteQueueAsync("orders", new OrderCreatedEvent { OrderId = 1 }, messageType: "OrderCreated");

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedOrders);
    }

    [Fact]
    public async Task ExecuteQueueAsync_MultipleMessages_ShouldInvokeFunctionForEach()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        await dispatcher.ExecuteQueueAsync("orders", new OrderCreatedEvent { OrderId = 1, CustomerName = "Bob" });
        await dispatcher.ExecuteQueueAsync("orders", new OrderCreatedEvent { OrderId = 2, CustomerName = "Carol" });

        Assert.Equal(2, OrderAzureServiceBusFunctions.ProcessedOrders.Count);
        Assert.Equal(1, OrderAzureServiceBusFunctions.ProcessedOrders[0].OrderId);
        Assert.Equal(2, OrderAzureServiceBusFunctions.ProcessedOrders[1].OrderId);
    }

    [Fact]
    public async Task ExecuteQueueAsync_WithApplicationProperties_ShouldInvokeFunction()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        var props = new Dictionary<string, object> { ["correlationId"] = "abc-123" };

        await dispatcher.ExecuteQueueAsync("orders", new OrderCreatedEvent { OrderId = 7 },
            applicationProperties: props);

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedOrders);
        Assert.Equal(7, OrderAzureServiceBusFunctions.ProcessedOrders[0].OrderId);
    }

    [Fact]
    public async Task ExecuteQueueAsync_UnknownQueue_ShouldThrow()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.ExecuteQueueAsync("unknown-queue", new { }));
    }

    // ── Topic execution ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteTopicAsync_NoSubscriptionFilter_ShouldInvokeAllSubscriptions()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        await dispatcher.ExecuteTopicAsync("events", new { EventId = 1 }, messageType: "OrderShipped");

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects);
        Assert.Equal("OrderShipped", OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects[0]);
        Assert.Single(OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects);
        Assert.Equal("OrderShipped", OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects[0]);
    }

    [Fact]
    public async Task ExecuteTopicAsync_SpecificSubscription_ShouldInvokeOnlyThatSubscription()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        await dispatcher.ExecuteTopicAsync("events", new { EventId = 2 },
            subscriptionName: "integration-tests-sub",
            messageType: "OrderCancelled");

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects);
        Assert.Equal("OrderCancelled", OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects[0]);
        Assert.Empty(OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects);
    }

    [Fact]
    public async Task ExecuteTopicAsync_SpecificAnalyticsSubscription_ShouldInvokeOnlyAnalytics()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        await dispatcher.ExecuteTopicAsync("events", new { EventId = 3 },
            subscriptionName: "analytics-sub",
            messageType: "OrderUpdated");

        Assert.Empty(OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects);
        Assert.Single(OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects);
        Assert.Equal("OrderUpdated", OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects[0]);
    }

    [Fact]
    public async Task ExecuteTopicAsync_UnknownTopic_ShouldThrow()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.ExecuteTopicAsync("unknown-topic", new { }));
    }

    [Fact]
    public async Task ExecuteTopicAsync_UnknownSubscription_ShouldThrow()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.ExecuteTopicAsync("events", new { }, subscriptionName: "nonexistent-sub"));
    }
    
       // ── Session-enabled queue ─────────────────────────────────────────────────

    [Fact]
    public async Task SessionQueue_FunctionReceivesOrder_AndSetsSessionState()
    {
        // Arrange
        OrderAzureServiceBusFunctions.SessionProcessedOrders.Clear();

        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        var order = new OrderCreatedEvent { OrderId = 42, CustomerName = "Alice", TotalAmount = 99.99m };

        // Act
        var result = await dispatcher.ExecuteQueueAsync("session-orders", order);

        // Assert — function executed
        Assert.Single(OrderAzureServiceBusFunctions.SessionProcessedOrders);
        Assert.Equal(42, OrderAzureServiceBusFunctions.SessionProcessedOrders[0].OrderId);

        // Assert — session mock captured state set by the function
        Assert.NotNull(result.SessionMessageActions);
        Assert.NotNull(result.SessionMessageActions.SessionState);
        Assert.Equal("42", result.SessionMessageActions.SessionState.ToString());

        // Assert — message was completed via MessageActions
        Assert.Single(result.MessageActions.CompletedMessages);
    }

    [Fact]
    public async Task SessionQueue_IsSessionsEnabled_DetectedCorrectly()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        var order = new OrderCreatedEvent { OrderId = 1, CustomerName = "Bob", TotalAmount = 10m };

        var result = await dispatcher.ExecuteQueueAsync("session-orders", order);

        // SessionMessageActions must be a non-null MockAzureServiceBusSessionMessageActions
        Assert.NotNull(result.SessionMessageActions);
    }

    // ── Batched queue ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchedQueue_DispatchesBatch_AllMessagesCompleted()
    {
        // Arrange
        OrderAzureServiceBusFunctions.BatchReceivedMessages.Clear();

        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        var orders = new[]
        {
            new OrderCreatedEvent { OrderId = 1, CustomerName = "Carol", TotalAmount = 10m },
            new OrderCreatedEvent { OrderId = 2, CustomerName = "Dave",  TotalAmount = 20m },
            new OrderCreatedEvent { OrderId = 3, CustomerName = "Eve",   TotalAmount = 30m },
        };

        // Act
        var result = await dispatcher.ExecuteBatchQueueAsync("batch-orders", orders);

        // Assert — function received the full batch
        Assert.Equal(3, OrderAzureServiceBusFunctions.BatchReceivedMessages.Count);

        // Assert — all messages were completed
        Assert.Equal(3, result.MessageActions.CompletedMessages.Count);

        // No session actions for a non-session function
        Assert.Null(result.SessionMessageActions);
    }

    [Fact]
    public async Task BatchedQueue_SingleMessage_WorksAsMinimalBatch()
    {
        OrderAzureServiceBusFunctions.BatchReceivedMessages.Clear();

        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        var orders = new[] { new OrderCreatedEvent { OrderId = 99, CustomerName = "Frank", TotalAmount = 5m } };

        var result = await dispatcher.ExecuteBatchQueueAsync("batch-orders", orders);

        Assert.Single(OrderAzureServiceBusFunctions.BatchReceivedMessages);
        Assert.Single(result.MessageActions.CompletedMessages);
    }

    // ── Return value / output binding ─────────────────────────────────────────

    [Fact]
    public async Task ForwardOrderQueue_FunctionReturnsForwardedEvent()
    {
        // Arrange
        OrderAzureServiceBusFunctions.ForwardedOrders.Clear();

        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        var order = new OrderCreatedEvent { OrderId = 7, CustomerName = "Grace", TotalAmount = 55m };

        // Act
        var result = await dispatcher.ExecuteQueueAsync("forward-orders", order);

        // Assert — function captured the order
        Assert.Single(OrderAzureServiceBusFunctions.ForwardedOrders);
        Assert.Equal(7, OrderAzureServiceBusFunctions.ForwardedOrders[0].OrderId);

        // Assert — return value is the forwarded event
        Assert.NotNull(result.ReturnValue);
        var forwarded = Assert.IsType<OrderForwardedEvent>(result.ReturnValue);
        Assert.Equal(7, forwarded.OriginalOrderId);
        Assert.Equal("Grace", forwarded.CustomerName);
    }

    // ── Manual settlement (DeadLetter, Complete) ──────────────────────────────

    [Fact]
    public async Task ManualActionsQueue_ValidOrder_IsCompleted()
    {
        // Arrange
        OrderAzureServiceBusFunctions.ActionProcessedOrders.Clear();

        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        var order = new OrderCreatedEvent { OrderId = 10, CustomerName = "Henry", TotalAmount = 100m };

        // Act
        var result = await dispatcher.ExecuteQueueAsync("orders-manual", order);

        // Assert — function processed the order
        Assert.Single(OrderAzureServiceBusFunctions.ActionProcessedOrders);
        Assert.Equal(10, OrderAzureServiceBusFunctions.ActionProcessedOrders[0].OrderId);

        // Assert — message was completed
        Assert.Single(result.MessageActions.CompletedMessages);
        Assert.Empty(result.MessageActions.DeadLetteredMessages);
    }

    [Fact]
    public async Task ManualActionsQueue_InvalidOrder_IsDeadLettered()
    {
        // Arrange
        OrderAzureServiceBusFunctions.ActionProcessedOrders.Clear();

        var dispatcher = _factory.CreateAzureServiceBusFunctionExecutor();
        // TotalAmount = 0 should trigger dead-lettering
        var invalidOrder = new OrderCreatedEvent { OrderId = 0, CustomerName = "Invalid", TotalAmount = 0 };

        // Act
        var result = await dispatcher.ExecuteQueueAsync("orders-manual", invalidOrder);

        // Assert — function did NOT add to the processed list
        Assert.Empty(OrderAzureServiceBusFunctions.ActionProcessedOrders);

        // Assert — message was dead-lettered
        Assert.Single(result.MessageActions.DeadLetteredMessages);
        Assert.Empty(result.MessageActions.CompletedMessages);

        var (_, reason, description, _) = result.MessageActions.DeadLetteredMessages[0];
        Assert.Equal("InvalidOrder", reason);
        Assert.Equal("TotalAmount must be positive", description);
    }

    // ── MessageActions: Abandon and Defer ─────────────────────────────────────

    [Fact]
    public async Task MessageActions_AbandonedMessages_RecordedCorrectly()
    {
        // Verify that if we had a function calling AbandonMessageAsync the spy records it.
        // We use a standalone mock directly to verify the mock's tracking.
        var mock = new MockAzureServiceBusMessageActions();
        var msg = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "test-1");

        await mock.AbandonMessageAsync(msg);

        Assert.Single(mock.AbandonedMessages);
        Assert.Same(msg, mock.AbandonedMessages[0].Message);
        Assert.Null(mock.AbandonedMessages[0].Properties);
    }

    [Fact]
    public async Task MessageActions_DeferredMessages_RecordedWithProperties()
    {
        var mock = new MockAzureServiceBusMessageActions();
        var msg = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "test-2");
        var props = new Dictionary<string, object> { ["retryCount"] = 3 };

        await mock.DeferMessageAsync(msg, props);

        Assert.Single(mock.DeferredMessages);
        Assert.Equal(3, mock.DeferredMessages[0].Properties!["retryCount"]);
    }

    // ── SessionMessageActions spy ─────────────────────────────────────────────

    [Fact]
    public async Task SessionMessageActions_GetSetState_RoundTrips()
    {
        var mock = new MockAzureServiceBusSessionMessageActions();
        var state = BinaryData.FromString("session-value-42");

        await mock.SetSessionStateAsync(state);
        var retrieved = await mock.GetSessionStateAsync();

        Assert.Equal("session-value-42", retrieved.ToString());
        Assert.Equal("session-value-42", mock.SessionState!.ToString());
    }

    [Fact]
    public async Task SessionMessageActions_RenewSessionLock_TrackedCorrectly()
    {
        var mock = new MockAzureServiceBusSessionMessageActions();

        Assert.False(mock.SessionLockRenewed);
        await mock.RenewSessionLockAsync();
        Assert.True(mock.SessionLockRenewed);
    }

    // ── Environment-variable queue name (%TestQueueName%) ────────────────────

    /// <summary>
    /// Verifies that a queue name configured as <c>%TestQueueName%</c> on the
    /// <c>ServiceBusTriggerAttribute</c> is resolved from the environment variable
    /// and the function is discovered and invoked correctly.
    /// </summary>
    [Fact]
    public async Task ExecuteQueueAsync_EnvVarQueueName_FunctionDiscoveredAndInvoked()
    {
        // Arrange – set the environment variable BEFORE the factory resolves function metadata.
        // Because FunctionAppFactory is a class fixture (shared), the env var must be set prior
        // to the first test run. Here we use a dedicated factory to guarantee isolation.
        const string resolvedQueueName = "env-test-queue";
        Environment.SetEnvironmentVariable("TestQueueName", resolvedQueueName);

        using var isolatedFactory = new FunctionAppFactory<Program>();
        var dispatcher = isolatedFactory.CreateAzureServiceBusFunctionExecutor();

        var order = new OrderCreatedEvent { OrderId = 99, CustomerName = "EnvTest", TotalAmount = 1m };

        // Act
        await dispatcher.ExecuteQueueAsync(resolvedQueueName, order);

        // Assert
        Assert.Single(OrderAzureServiceBusFunctions.EnvQueueProcessedOrders);
        Assert.Equal(99, OrderAzureServiceBusFunctions.EnvQueueProcessedOrders[0].OrderId);

        // Cleanup
        Environment.SetEnvironmentVariable("TestQueueName", null);
    }

    /// <summary>
    /// Verifies that when the <c>TestQueueName</c> environment variable is not set,
    /// the executor cannot find the function (the raw <c>%TestQueueName%</c> placeholder
    /// is not registered as a known queue).
    /// </summary>
    [Fact]
    public async Task ExecuteQueueAsync_EnvVarNotSet_ThrowsInvalidOperationException()
    {
        // Ensure the env var is absent
        Environment.SetEnvironmentVariable("TestQueueName", null);

        using var isolatedFactory = new FunctionAppFactory<Program>();
        var dispatcher = isolatedFactory.CreateAzureServiceBusFunctionExecutor();

        var order = new OrderCreatedEvent { OrderId = 1, CustomerName = "Test", TotalAmount = 1m };

        // The raw placeholder "%TestQueueName%" is not a valid resolved queue name,
        // so executing it should throw.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.ExecuteQueueAsync("env-test-queue", order));
    }
}

