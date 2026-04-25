using AzureFunctions.IntegrationTests.Durable;
using AzureFunctions.IntegrationTests.Mocks.Durable;

namespace AzureFunctions.IntegrationTests.SampleApp.Tests;

/// <summary>
/// Integration tests for <see cref="DurableFunctionExecutor"/>.
/// Covers activity invocation (typed input/output, untyped input), orchestrator invocation,
/// mock-activity configuration, and error handling — all executed in-process without a
/// live Durable Task hub.
/// </summary>
public class DurableFunctionIntegrationTests : IClassFixture<FunctionAppFactory<Program>>, IDisposable
{
    private readonly FunctionAppFactory<Program> _factory;

    public DurableFunctionIntegrationTests(FunctionAppFactory<Program> factory)
    {
        _factory = factory;
        ResetState();
    }

    public void Dispose() => ResetState();

    private static void ResetState()
    {
        OrderDurableFunctions.ProcessedOrders.Clear();
        OrderDurableFunctions.SentEmails.Clear();
    }

    // ── Activity: typed input + typed output ──────────────────────────────────

    /// <summary>
    /// Verifies that an activity function with a typed input and boolean return value
    /// is invoked correctly and returns the expected result.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_ValidOrder_ReturnsTrue()
    {
        var executor = _factory.CreateDurableFunctionExecutor();
        var order = new OrderPayload(OrderId: 1, CustomerEmail: "alice@example.com", Amount: 99.99m);

        var result = await executor.ExecuteActivityAsync<OrderPayload, bool>(
            "ProcessOrderActivity", order);

        Assert.True(result);
        Assert.Single(OrderDurableFunctions.ProcessedOrders);
        Assert.Equal(1, OrderDurableFunctions.ProcessedOrders[0].OrderId);
    }

    /// <summary>
    /// Verifies that an activity returns <see langword="false"/> when the order has
    /// a non-positive amount (invalid order guard logic).
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_InvalidOrder_ReturnsFalse()
    {
        var executor = _factory.CreateDurableFunctionExecutor();
        var invalidOrder = new OrderPayload(OrderId: 0, CustomerEmail: "bad@example.com", Amount: 0m);

        var result = await executor.ExecuteActivityAsync<OrderPayload, bool>(
            "ProcessOrderActivity", invalidOrder);

        Assert.False(result);
        Assert.Empty(OrderDurableFunctions.ProcessedOrders);
    }

    // ── Activity: void return (fire-and-forget) ───────────────────────────────

    /// <summary>
    /// Verifies that a void-return activity (no typed output) is invoked and produces
    /// the expected side-effect without throwing.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_VoidReturn_SideEffectRecorded()
    {
        var executor = _factory.CreateDurableFunctionExecutor();

        await executor.ExecuteActivityAsync<string>(
            "SendConfirmationActivity", "bob@example.com");

        Assert.Single(OrderDurableFunctions.SentEmails);
        Assert.Equal("bob@example.com", OrderDurableFunctions.SentEmails[0]);
    }

    // ── Activity: untyped input ───────────────────────────────────────────────

    /// <summary>
    /// Verifies that the untyped-input overload (<c>InvokeActivityAsync&lt;TResult&gt;</c>)
    /// JSON-round-trips the input to the activity's parameter type and returns the correct result.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_UntypedInput_DeserializesCorrectly()
    {
        var executor = _factory.CreateDurableFunctionExecutor();
        var raw = new { orderId = 5, customerEmail = "carol@example.com", amount = 50.0m };

        var result = await executor.ExecuteActivityAsync<bool>(
            "ProcessOrderActivity", raw);

        Assert.True(result);
        Assert.Single(OrderDurableFunctions.ProcessedOrders);
        Assert.Equal(5, OrderDurableFunctions.ProcessedOrders[0].OrderId);
    }

    // ── Activity: wrong kind throws ───────────────────────────────────────────

    /// <summary>
    /// Verifies that invoking an orchestrator function via the activity overload throws
    /// <see cref="InvalidOperationException"/> with a descriptive message.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_OrchestratorName_ThrowsInvalidOperation()
    {
        var executor = _factory.CreateDurableFunctionExecutor();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteActivityAsync<OrderPayload, bool>(
                "ProcessOrderOrchestrator",
                new OrderPayload(1, "x@x.com", 10m)));
    }

    // ── Orchestrator: full end-to-end ─────────────────────────────────────────

    /// <summary>
    /// Verifies that an orchestrator calling two activities in sequence returns the correct
    /// final result when both activities are mocked to succeed.
    /// </summary>
    [Fact]
    public async Task InvokeOrchestratorAsync_ValidOrder_ReturnsTrueAndSendsEmail()
    {
        var executor = _factory.CreateDurableFunctionExecutor();
        var order = new OrderPayload(OrderId: 42, CustomerEmail: "dave@example.com", Amount: 150m);

        var context = new MockTaskOrchestrationContext(input: order)
            .MockActivity<OrderPayload, bool>("ProcessOrderActivity", _ => true)
            .MockActivity<string>("SendConfirmationActivity", _ => { });  // void activity

        var result = await executor.ExecuteOrchestratorAsync<bool>(
            "ProcessOrderOrchestrator", context);

        Assert.True(result);
    }

    /// <summary>
    /// Verifies that when the first activity returns <see langword="false"/> the orchestrator
    /// short-circuits and returns <see langword="false"/> without calling the second activity.
    /// </summary>
    [Fact]
    public async Task InvokeOrchestratorAsync_ActivityReturnsFalse_OrchestratorReturnsFalse()
    {
        var executor = _factory.CreateDurableFunctionExecutor();
        var invalidOrder = new OrderPayload(OrderId: 0, CustomerEmail: "eve@example.com", Amount: 0m);

        var context = new MockTaskOrchestrationContext(input: invalidOrder)
            .MockActivity<OrderPayload, bool>("ProcessOrderActivity", _ => false);

        var result = await executor.ExecuteOrchestratorAsync<bool>(
            "ProcessOrderOrchestrator", context);

        Assert.False(result);
    }

    // ── Orchestrator: void return ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that the void-return orchestrator overload runs to completion without throwing.
    /// </summary>
    [Fact]
    public async Task InvokeOrchestratorAsync_VoidReturn_RunsWithoutException()
    {
        var executor = _factory.CreateDurableFunctionExecutor();
        var order = new OrderPayload(OrderId: 7, CustomerEmail: "frank@example.com", Amount: 75m);

        var context = new MockTaskOrchestrationContext(input: order)
            .MockActivity<OrderPayload, bool>("ProcessOrderActivity", _ => true)
            .MockActivity<string>("SendConfirmationActivity", _ => { });  // void activity

        // Should not throw
        await executor.ExecuteOrchestratorAsync("ProcessOrderOrchestrator", context);
    }

    // ── Orchestrator: unmocked activity throws ────────────────────────────────

    /// <summary>
    /// Verifies that calling an activity without a matching mock registered on
    /// <see cref="MockTaskOrchestrationContext"/> throws <see cref="InvalidOperationException"/>
    /// with a descriptive message guiding the developer.
    /// </summary>
    [Fact]
    public async Task InvokeOrchestratorAsync_UnmockedActivity_Throws()
    {
        var executor = _factory.CreateDurableFunctionExecutor();
        var order = new OrderPayload(OrderId: 3, CustomerEmail: "grace@example.com", Amount: 30m);

        // No MockActivity registered — the orchestrator will try to call ProcessOrderActivity
        var context = new MockTaskOrchestrationContext(input: order);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteOrchestratorAsync<bool>("ProcessOrderOrchestrator", context));
    }

    // ── MockTaskOrchestrationContext: CustomStatus ────────────────────────────

    /// <summary>
    /// Verifies that <see cref="MockTaskOrchestrationContext.CustomStatus"/> captures
    /// whatever value the orchestrator passes to <c>SetCustomStatus</c>.
    /// </summary>
    [Fact]
    public void MockContext_SetCustomStatus_IsCaptured()
    {
        var context = new MockTaskOrchestrationContext();

        context.SetCustomStatus("processing");

        Assert.Equal("processing", context.CustomStatus);
    }

    // ── MockTaskActivityContext ───────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="MockTaskActivityContext"/> exposes the supplied function name
    /// and instance ID so activity code can read them during tests.
    /// </summary>
    [Fact]
    public void MockActivityContext_ExposesNameAndInstanceId()
    {
        var ctx = new MockTaskActivityContext("MyActivity", "inst-001");

        Assert.Equal("MyActivity", ctx.Name.Name);
        Assert.Equal("inst-001", ctx.InstanceId);
    }

    /// <summary>
    /// Verifies that a new GUID instance ID is generated when none is supplied.
    /// </summary>
    [Fact]
    public void MockActivityContext_AutoGeneratesInstanceId()
    {
        var ctx = new MockTaskActivityContext("MyActivity");

        Assert.False(string.IsNullOrEmpty(ctx.InstanceId));
        Assert.True(Guid.TryParse(ctx.InstanceId, out _));
    }
}

