using System.Net.Http;
using System.Text;

namespace AzureFunctions.IntegrationTests.Http;

/// <summary>
/// Custom HTTP message handler that routes requests to Azure Functions
/// </summary>
internal class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly dynamic _factory;

    public TestHttpMessageHandler(dynamic factory)
    {
        _factory = factory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Extract the path from the request
        var path = request.RequestUri?.AbsolutePath?.TrimStart('/') ?? "";

        // Remove 'api/' prefix if present to match Azure Functions routing
        if (path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(4);
        }

        var method = request.Method.Method;

        // Extract headers from HttpRequestMessage
        var headers = new Dictionary<string, string>();
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        // Extract body content if present
        string? body = null;
        if (request.Content != null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        // Extract query string if present
        var queryString = request.RequestUri?.Query;

        // Execute the function
        var response = await _factory.ExecuteFunctionAsync(path, method, headers, body, queryString);

        return new HttpResponseMessage(response.StatusCode)
        {
            Content = new StringContent(response.Content ?? "", Encoding.UTF8, "application/json")
        };
    }
}
