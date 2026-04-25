using Azure.Messaging.ServiceBus;
using AzureFunctions.IntegrationTests.AzureServiceBus;
using AzureFunctions.IntegrationTests.Mocks.AzureServiceBus;

namespace AzureFunctions.IntegrationTests.SampleApp.Tests;

/// <summary>
/// Integration tests for <see cref="AzureServiceBusExecutor"/>.
/// Covers queue execution, topic execution, session-enabled queues, batched queues,
/// return-value / output bindings, manual message settlement (Complete / DeadLetter / Abandon / Defer),
/// environment-variable queue name resolution, and direct <see cref="ServiceBusReceivedMessage"/> overloads.
/// Functions are executed in-process without a real Azure Service Bus connection.
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

    /// <summary>
    /// Resets all static state on <see cref="OrderAzureServiceBusFunctions"/> between tests
    /// to ensure full isolation regardless of execution order.
    /// </summary>
    private static void ResetState()
    {
        OrderAzureServiceBusFunctions.ProcessedOrders.Clear();
        OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects.Clear();
        OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects.Clear();
        OrderAzureServiceBusFunctions.SessionProcessedOrders.Clear();
        OrderAzureServiceBusFunctions.BatchReceivedMessages.Clear();
        OrderAzureServiceBusFunctions.ForwardedOrders.Clear();
        OrderAzureServiceBusFunctions.ActionProcessedOrders.Clear();
        OrderAzureServiceBusFunctions.EnvQueueProcessedOrders.Clear();
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteTopicAsync_UnknownTopic_ShouldThrow()
    {
        var executor = _factory.CreateAzureServiceBusExecutor();
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(new { }),
            subject: "order.direct",
            messageId: "msg-direct-001",
            correlationId: "corr-001");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteTopicAsync<object>("unknown-topic", rawMessage));
    }

    [Fact]
    public async Task ExecuteTopicAsync_UnknownSubscription_ShouldThrow()
    {
        var executor = _factory.CreateAzureServiceBusExecutor();
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(new { }),
            subject: "order.direct",
            messageId: "msg-direct-001",
            correlationId: "corr-001");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteTopicAsync<object>("events", rawMessage, subscriptionName: "nonexistent-sub"));
    }

    // ── Session-enabled queue ─────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a session-enabled queue trigger function is executed, receives the typed order,
    /// stores session state, and completes the message via <see cref="MockAzureServiceBusMessageActions"/>.
    /// </summary>
    [Fact]
    public async Task SessionQueue_FunctionReceivesOrder_AndSetsSessionState()
    {
        // Arrange
        OrderAzureServiceBusFunctions.SessionProcessedOrders.Clear();

        var executor = _factory.CreateAzureServiceBusExecutor();
        var order = new OrderCreatedEvent { OrderId = 42, CustomerName = "Alice", TotalAmount = 99.99m };
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(order),
            subject: "order.session",
            messageId: "msg-session-001",
            correlationId: "corr-session-001");

        // Act
        var result = await executor.ExecuteQueueAsync<object>("session-orders", rawMessage);

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

    /// <summary>
    /// Verifies that <see cref="AzureServiceBusExecutionResult{T}.SessionMessageActions"/> is non-null
    /// when the triggered function is configured with <c>IsSessionsEnabled = true</c>.
    /// </summary>
    [Fact]
    public async Task SessionQueue_IsSessionsEnabled_SessionActionsArePopulated()
    {
        var executor = _factory.CreateAzureServiceBusExecutor();
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(
                new OrderCreatedEvent { OrderId = 1, CustomerName = "Bob", TotalAmount = 10m }),
            messageId: "msg-session-002");

        var result = await executor.ExecuteQueueAsync<object>("session-orders", rawMessage);

        // SessionMessageActions must be a non-null MockAzureServiceBusSessionMessageActions
        Assert.NotNull(result.SessionMessageActions);
    }

    // ── Batched queue ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a batch of <see cref="ServiceBusReceivedMessage"/> instances is forwarded to
    /// the function in full and that all messages are completed via <see cref="MockAzureServiceBusMessageActions"/>.
    /// </summary>
    [Fact]
    public async Task BatchedQueue_DispatchesBatch_AllMessagesCompleted()
    {
        // Arrange
        OrderAzureServiceBusFunctions.BatchReceivedMessages.Clear();

        var executor = _factory.CreateAzureServiceBusExecutor();
        var rawMessages = new[]
        {
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 1, CustomerName = "Carol", TotalAmount = 10m }),
                messageId: "batch-001"),
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 2, CustomerName = "Dave", TotalAmount = 20m }),
                messageId: "batch-002"),
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 3, CustomerName = "Eve", TotalAmount = 30m }),
                messageId: "batch-003"),
        };


        // Act
        var result = await executor.ExecuteBatchQueueAsync<object>("batch-orders", rawMessages);

        // Assert — function received the full batch
        Assert.Equal(3, OrderAzureServiceBusFunctions.BatchReceivedMessages.Count);

        // Assert — all messages were completed
        Assert.Equal(3, result.MessageActions.CompletedMessages.Count);

        // No session actions for a non-session function
        Assert.Null(result.SessionMessageActions);
    }

    /// <summary>
    /// Verifies that a single-item batch is treated as a valid minimal batch.
    /// </summary>
    [Fact]
    public async Task BatchedQueue_SingleMessage_WorksAsMinimalBatch()
    {
        OrderAzureServiceBusFunctions.BatchReceivedMessages.Clear();

        var executor = _factory.CreateAzureServiceBusExecutor();
        var rawMessages = new[]
        {
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 99, CustomerName = "Frank", TotalAmount = 5m }),
                messageId: "batch-single-001"),
        };

        var result = await executor.ExecuteBatchQueueAsync<object>("batch-orders", rawMessages);

        Assert.Single(OrderAzureServiceBusFunctions.BatchReceivedMessages);
        Assert.Single(result.MessageActions.CompletedMessages);
    }

    // ── Return value / output binding ─────────────────────────────────────────

    /// <summary>
    /// Verifies that a function's return value (output binding) is captured in
    /// <see cref="AzureServiceBusExecutionResult{T}.ReturnValue"/> as a strongly-typed value,
    /// without requiring any cast in the test.
    /// </summary>
    [Fact]
    public async Task ForwardOrderQueue_FunctionReturnsForwardedEvent()
    {
        // Arrange
        OrderAzureServiceBusFunctions.ForwardedOrders.Clear();

        var executor = _factory.CreateAzureServiceBusExecutor();
        var order = new OrderCreatedEvent { OrderId = 7, CustomerName = "Grace", TotalAmount = 55m };
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(order),
            messageId: "forward-001");

        // Act — specify the expected return type as the generic argument
        var result = await executor.ExecuteQueueAsync<OrderForwardedEvent>("forward-orders", rawMessage);

        // Assert — function captured the order
        Assert.Single(OrderAzureServiceBusFunctions.ForwardedOrders);
        Assert.Equal(7, OrderAzureServiceBusFunctions.ForwardedOrders[0].OrderId);

        // Assert — ReturnValue is already strongly typed; no cast required
        Assert.NotNull(result.ReturnValue);
        Assert.Equal(7, result.ReturnValue.OriginalOrderId);
        Assert.Equal("Grace", result.ReturnValue.CustomerName);
    }

    // ── Manual settlement (Complete / DeadLetter) ─────────────────────────────

    /// <summary>
    /// Verifies that a valid order triggers <c>CompleteMessageAsync</c> and the message
    /// is recorded in <see cref="MockAzureServiceBusMessageActions.CompletedMessages"/>.
    /// </summary>
    [Fact]
    public async Task ManualActionsQueue_ValidOrder_IsCompleted()
    {
        // Arrange
        OrderAzureServiceBusFunctions.ActionProcessedOrders.Clear();

        var executor = _factory.CreateAzureServiceBusExecutor();
        var order = new OrderCreatedEvent { OrderId = 10, CustomerName = "Henry", TotalAmount = 100m };
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(order),
            messageId: "manual-001");

        // Act
        var result = await executor.ExecuteQueueAsync<object>("orders-manual", rawMessage);

        // Assert — function processed the order
        Assert.Single(OrderAzureServiceBusFunctions.ActionProcessedOrders);
        Assert.Equal(10, OrderAzureServiceBusFunctions.ActionProcessedOrders[0].OrderId);

        // Assert — message was completed
        Assert.Single(result.MessageActions.CompletedMessages);
        Assert.Empty(result.MessageActions.DeadLetteredMessages);
    }

    /// <summary>
    /// Verifies that an order with <c>TotalAmount = 0</c> is dead-lettered with the expected
    /// reason and description, and is NOT added to the processed-orders collection.
    /// </summary>
    [Fact]
    public async Task ManualActionsQueue_InvalidOrder_IsDeadLettered()
    {
        // Arrange
        OrderAzureServiceBusFunctions.ActionProcessedOrders.Clear();

        var executor = _factory.CreateAzureServiceBusExecutor();
        // TotalAmount = 0 should trigger dead-lettering
        var invalidOrder = new OrderCreatedEvent { OrderId = 0, CustomerName = "Invalid", TotalAmount = 0 };
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(invalidOrder),
            messageId: "manual-invalid-001");

        // Act
        var result = await executor.ExecuteQueueAsync<object>("orders-manual", rawMessage);

        // Assert — function did NOT add to the processed list
        Assert.Empty(OrderAzureServiceBusFunctions.ActionProcessedOrders);

        // Assert — message was dead-lettered
        Assert.Single(result.MessageActions.DeadLetteredMessages);
        Assert.Empty(result.MessageActions.CompletedMessages);

        var (_, reason, description, _) = result.MessageActions.DeadLetteredMessages[0];
        Assert.Equal("InvalidOrder", reason);
        Assert.Equal("TotalAmount must be positive", description);
    }

    // ── MessageActions spy: Abandon and Defer ──────────────────────────────────

    /// <summary>
    /// Verifies that <c>AbandonMessageAsync</c> calls are recorded by
    /// <see cref="MockAzureServiceBusMessageActions.AbandonedMessages"/>.
    /// </summary>
    [Fact]
    public async Task MessageActions_AbandonedMessages_RecordedCorrectly()
    {
        var mock = new MockAzureServiceBusMessageActions();
        var msg = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "test-abandon-1");

        await mock.AbandonMessageAsync(msg);

        Assert.Single(mock.AbandonedMessages);
        Assert.Same(msg, mock.AbandonedMessages[0].Message);
        Assert.Null(mock.AbandonedMessages[0].Properties);
    }

    /// <summary>
    /// Verifies that <c>DeferMessageAsync</c> calls, including message properties to modify,
    /// are recorded by <see cref="MockAzureServiceBusMessageActions.DeferredMessages"/>.
    /// </summary>
    [Fact]
    public async Task MessageActions_DeferredMessages_RecordedWithProperties()
    {
        var mock = new MockAzureServiceBusMessageActions();
        var msg = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "test-defer-1");
        var props = new Dictionary<string, object> { ["retryCount"] = 3 };

        await mock.DeferMessageAsync(msg, props);

        Assert.Single(mock.DeferredMessages);
        Assert.Equal(3, mock.DeferredMessages[0].Properties!["retryCount"]);
    }

    // ── SessionMessageActions spy ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that <c>SetSessionStateAsync</c> / <c>GetSessionStateAsync</c> round-trips
    /// correctly through <see cref="MockAzureServiceBusSessionMessageActions"/>.
    /// </summary>
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

    /// <summary>
    /// Verifies that <c>RenewSessionLockAsync</c> sets
    /// <see cref="MockAzureServiceBusSessionMessageActions.SessionLockRenewed"/> to <see langword="true"/>.
    /// </summary>
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
    /// <see cref="ServiceBusTriggerAttribute"/> is resolved from the environment variable
    /// and the function is discovered and invoked correctly.
    /// An isolated <see cref="FunctionAppFactory{TEntryPoint}"/> is used to guarantee that
    /// the env variable is set before function metadata is discovered.
    /// </summary>
    [Fact]
    public async Task ExecuteQueueAsync_EnvVarQueueName_FunctionDiscoveredAndInvoked()
    {
        const string resolvedQueueName = "env-test-queue";
        Environment.SetEnvironmentVariable("TestQueueName", resolvedQueueName);

        try
        {
            using var isolatedFactory = new FunctionAppFactory<Program>();
            var executor = isolatedFactory.CreateAzureServiceBusExecutor();

            var order = new OrderCreatedEvent { OrderId = 99, CustomerName = "EnvTest", TotalAmount = 1m };
            var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromObjectAsJson(order),
                messageId: "env-001");

            await executor.ExecuteQueueAsync<object>(resolvedQueueName, rawMessage);

            Assert.Single(OrderAzureServiceBusFunctions.EnvQueueProcessedOrders);
            Assert.Equal(99, OrderAzureServiceBusFunctions.EnvQueueProcessedOrders[0].OrderId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TestQueueName", null);
        }
    }

    /// <summary>
    /// Verifies that when the <c>TestQueueName</c> environment variable is not set,
    /// the executor cannot find the function because the raw <c>%TestQueueName%</c> placeholder
    /// is never registered as a known queue name.
    /// </summary>
    [Fact]
    public async Task ExecuteQueueAsync_EnvVarNotSet_ThrowsInvalidOperationException()
    {
        Environment.SetEnvironmentVariable("TestQueueName", null);

        using var isolatedFactory = new FunctionAppFactory<Program>();
        var executor = isolatedFactory.CreateAzureServiceBusExecutor();

        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 1, CustomerName = "Test", TotalAmount = 1m }),
            messageId: "env-missing-001");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteQueueAsync<object>("env-test-queue", rawMessage));
    }

    // ── Direct ServiceBusReceivedMessage overloads ────────────────────────────

    /// <summary>
    /// Verifies that the <c>subject</c> set on a pre-built <see cref="ServiceBusReceivedMessage"/>
    /// is preserved and arrives unchanged at the topic-triggered function when a specific
    /// subscription is targeted.
    /// </summary>
    [Fact]
    public async Task ExecuteTopicAsync_PreBuiltMessage_SubjectPreserved()
    {
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            subject: "event.shipped");

        var executor = _factory.CreateAzureServiceBusExecutor();

        await executor.ExecuteTopicAsync<object>("events", rawMessage, subscriptionName: "integration-tests-sub");

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects);
        Assert.Equal("event.shipped", OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects[0]);
        Assert.Empty(OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects);
    }

    /// <summary>
    /// Verifies that omitting the subscription filter causes ALL topic subscriptions to be executed,
    /// each receiving the same pre-built message and subject.
    /// </summary>
    [Fact]
    public async Task ExecuteTopicAsync_PreBuiltMessage_NoFilter_AllSubscriptionsExecuted()
    {
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            subject: "event.broadcast");

        var executor = _factory.CreateAzureServiceBusExecutor();

        await executor.ExecuteTopicAsync<object>("events", rawMessage);

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects);
        Assert.Equal("event.broadcast", OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects[0]);
        Assert.Single(OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects);
        Assert.Equal("event.broadcast", OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects[0]);
    }

    /// <summary>
    /// Verifies that a batch of pre-built <see cref="ServiceBusReceivedMessage"/> instances
    /// passed to <see cref="IAzureServiceBusExecutor.ExecuteBatchQueueAsync"/>
    /// are forwarded verbatim to the function without re-serialisation.
    /// </summary>
    [Fact]
    public async Task ExecuteBatchQueueAsync_PreBuiltMessages_AllMessagesForwardedToFunction()
    {
        // Arrange
        OrderAzureServiceBusFunctions.BatchReceivedMessages.Clear();

        var messages = new[]
        {
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("""{"orderId":1}"""), messageId: "b-001"),
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("""{"orderId":2}"""), messageId: "b-002"),
        };

        var executor = _factory.CreateAzureServiceBusExecutor();

        // Act
        var result = await executor.ExecuteBatchQueueAsync<object>("batch-orders", messages);

        // Assert — function received both pre-built messages verbatim
        Assert.Equal(2, OrderAzureServiceBusFunctions.BatchReceivedMessages.Count);
        Assert.Equal(2, result.MessageActions.CompletedMessages.Count);
    }

    /// <summary>
    /// Verifies that <see cref="IAzureServiceBusExecutor.ExecuteBatchTopicAsync"/>
    /// is not yet supported for non-batched topic functions and throws
    /// <see cref="InvalidOperationException"/> when the matched subscription function
    /// is not configured with <c>IsBatched = true</c>.
    /// </summary>
    [Fact]
    public async Task ExecuteBatchTopicAsync_NonBatchedSubscription_Throws()
    {
        var messages = new[]
        {
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString("{}"), subject: "event.test", messageId: "bt-001"),
        };

        var executor = _factory.CreateAzureServiceBusExecutor();

        // The "events" topic subscriptions are not configured with IsBatched = true
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteBatchTopicAsync<object>("events", messages,
                subscriptionName: "integration-tests-sub"));
    }

    /// <summary>
    /// Verifies that application properties set on a pre-built <see cref="ServiceBusReceivedMessage"/>
    /// are forwarded to the function unchanged — the executor must not strip or alter message metadata.
    /// The function receives the full raw message via the trigger parameter so all properties are intact.
    /// </summary>
    [Fact]
    public async Task ExecuteQueueAsync_PreBuiltMessage_MessageMetadataPreserved()
    {
        var props = new Dictionary<string, object>
        {
            ["tenantId"] = "tenant-abc",
            ["priority"] = 1
        };
        var body = BinaryData.FromObjectAsJson(
            new { orderId = 77, customerName = "PropTest", totalAmount = 5.0 });
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: body,
            subject: "order.props",
            properties: props,
            messageId: "msg-props-001",
            correlationId: "corr-props-001");

        var executor = _factory.CreateAzureServiceBusExecutor();
        var result = await executor.ExecuteQueueAsync<object>("orders-manual", rawMessage);

        // The function deserialised the body and processed the order
        Assert.Single(OrderAzureServiceBusFunctions.ActionProcessedOrders);
        Assert.Equal(77, OrderAzureServiceBusFunctions.ActionProcessedOrders[0].OrderId);
        // Message was completed — metadata did not interfere with execution
        Assert.Single(result.MessageActions.CompletedMessages);
        // The completed-message reference is the exact same object passed in
        Assert.Same(rawMessage, result.MessageActions.CompletedMessages[0]);
    }

    // ── Non-generic convenience overloads ─────────────────────────────────────

    /// <summary>
    /// Verifies that the non-generic <c>ExecuteQueueAsync</c> overload (no type argument)
    /// executes the function and still provides access to <see cref="AzureServiceBusExecutionResult{T}.MessageActions"/>
    /// for settlement assertions, without requiring a type argument.
    /// </summary>
    [Fact]
    public async Task ExecuteQueueAsync_NonGeneric_MessageActionsAvailable()
    {
        var executor = _factory.CreateAzureServiceBusExecutor();
        var order = new OrderCreatedEvent { OrderId = 20, CustomerName = "Ivan", TotalAmount = 50m };
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(order),
            messageId: "ng-queue-001");

        // Act — no type argument needed
        var result = await executor.ExecuteQueueAsync("orders-manual", rawMessage);

        Assert.Single(OrderAzureServiceBusFunctions.ActionProcessedOrders);
        Assert.Equal(20, OrderAzureServiceBusFunctions.ActionProcessedOrders[0].OrderId);
        Assert.Single(result.MessageActions.CompletedMessages);
    }

    /// <summary>
    /// Verifies that the non-generic <c>ExecuteBatchQueueAsync</c> overload executes a batched
    /// function and returns <see cref="AzureServiceBusExecutionResult{T}.MessageActions"/>
    /// without requiring a type argument.
    /// </summary>
    [Fact]
    public async Task ExecuteBatchQueueAsync_NonGeneric_MessageActionsAvailable()
    {
        OrderAzureServiceBusFunctions.BatchReceivedMessages.Clear();

        var executor = _factory.CreateAzureServiceBusExecutor();
        var messages = new[]
        {
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 1, CustomerName = "Jack", TotalAmount = 10m }),
                messageId: "ng-batch-001"),
            ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromObjectAsJson(new OrderCreatedEvent { OrderId = 2, CustomerName = "Kate", TotalAmount = 20m }),
                messageId: "ng-batch-002"),
        };

        // Act — no type argument needed
        var result = await executor.ExecuteBatchQueueAsync("batch-orders", messages);

        Assert.Equal(2, OrderAzureServiceBusFunctions.BatchReceivedMessages.Count);
        Assert.Equal(2, result.MessageActions.CompletedMessages.Count);
    }

    /// <summary>
    /// Verifies that the non-generic <c>ExecuteTopicAsync</c> overload with a specific subscription
    /// executes the correct subscription function without requiring a type argument.
    /// </summary>
    [Fact]
    public async Task ExecuteTopicAsync_NonGeneric_SpecificSubscription_Executed()
    {
        var executor = _factory.CreateAzureServiceBusExecutor();
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            subject: "event.ng-test",
            messageId: "ng-topic-001");

        // Act — no type argument needed
        var result = await executor.ExecuteTopicAsync("events", rawMessage,
            subscriptionName: "integration-tests-sub");

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects);
        Assert.Equal("event.ng-test", OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects[0]);
        Assert.Empty(OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects);
        Assert.NotNull(result.MessageActions);
    }

    /// <summary>
    /// Verifies that the non-generic <c>ExecuteTopicAsync</c> overload without a subscription filter
    /// executes ALL topic subscriptions without requiring a type argument.
    /// </summary>
    [Fact]
    public async Task ExecuteTopicAsync_NonGeneric_NoFilter_AllSubscriptionsExecuted()
    {
        var executor = _factory.CreateAzureServiceBusExecutor();
        var rawMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"),
            subject: "event.ng-broadcast",
            messageId: "ng-topic-002");

        // Act — no type argument, no subscription filter
        await executor.ExecuteTopicAsync("events", rawMessage);

        Assert.Single(OrderAzureServiceBusFunctions.ProcessedIntegrationSubjects);
        Assert.Single(OrderAzureServiceBusFunctions.ProcessedAnalyticsSubjects);
    }
}


