using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace AzureFunctions.IntegrationTests.SampleApp;

/// <summary>
/// Sample Durable Functions demonstrating orchestrator and activity patterns
/// for use in integration tests via <c>FunctionAppFactory.CreateDurableFunctionExecutor()</c>.
/// </summary>
public class OrderDurableFunctions
{
    // ── Shared in-memory state (inspectable from tests) ───────────────────────

    /// <summary>Orders processed by <c>ProcessOrderActivity</c> during the current test run.</summary>
    public static readonly List<OrderPayload> ProcessedOrders = new();

    /// <summary>Emails sent by <c>SendConfirmationActivity</c> during the current test run.</summary>
    public static readonly List<string> SentEmails = new();

    // ── Orchestrator ──────────────────────────────────────────────────────────

    /// <summary>
    /// Orchestrator that processes an order end-to-end: validates it via
    /// <c>ProcessOrderActivity</c>, then sends a confirmation via <c>SendConfirmationActivity</c>.
    /// Returns <see langword="true"/> when both activities succeed.
    /// </summary>
    [Function("ProcessOrderOrchestrator")]
    public async Task<bool> ProcessOrderOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var order = context.GetInput<OrderPayload>()
            ?? throw new InvalidOperationException("Order payload is required.");

        var processed = await context.CallActivityAsync<bool>(
            "ProcessOrderActivity", order);

        if (!processed)
            return false;

        await context.CallActivityAsync(
            "SendConfirmationActivity", order.CustomerEmail);

        return true;
    }

    // ── Activities ────────────────────────────────────────────────────────────

    /// <summary>
    /// Activity that validates and records an order.
    /// Returns <see langword="false"/> when <see cref="OrderPayload.Amount"/> is non-positive.
    /// </summary>
    [Function("ProcessOrderActivity")]
    public bool ProcessOrderActivity(
        [ActivityTrigger] OrderPayload order,
        FunctionContext context)
    {
        if (order.Amount <= 0)
            return false;

        ProcessedOrders.Add(order);
        return true;
    }

    /// <summary>
    /// Activity that records a confirmation email to <paramref name="email"/>.
    /// </summary>
    [Function("SendConfirmationActivity")]
    public void SendConfirmationActivity(
        [ActivityTrigger] string email,
        FunctionContext context)
    {
        SentEmails.Add(email);
    }
}

/// <summary>Order payload used by Durable Function activities and orchestrators.</summary>
public record OrderPayload(int OrderId, string CustomerEmail, decimal Amount);

