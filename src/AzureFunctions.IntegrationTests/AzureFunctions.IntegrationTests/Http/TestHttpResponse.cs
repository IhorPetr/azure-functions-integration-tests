using System.Net;

namespace AzureFunctions.IntegrationTests.Http;

/// <summary>
/// Represents an HTTP response from a test function execution
/// </summary>
public class TestHttpResponse
{
    /// <summary>
    /// Gets or sets the HTTP status code
    /// </summary>
    public HttpStatusCode StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the response content as a string
    /// </summary>
    public string? Content { get; set; }
}
