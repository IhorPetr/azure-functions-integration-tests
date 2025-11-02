using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Claims;

namespace AzureFunctions.IntegrationTests.Mocks;

/// <summary>
/// Simple mock implementation of HttpRequestData for testing
/// </summary>
public class SimpleHttpRequestData : HttpRequestData
{
    private readonly MemoryStream _bodyStream;
    private readonly Uri _url;

    /// <summary>
    /// Initializes a new instance of SimpleHttpRequestData
    /// </summary>
    /// <param name="headers">Optional headers to include in the request</param>
    /// <param name="body">Optional body content</param>
    /// <param name="route">The route being requested</param>
    /// <param name="queryString">Optional query string parameters</param>
    public SimpleHttpRequestData(
        Dictionary<string, string>? headers = null,
        string? body = null,
        string? route = null,
        string? queryString = null)
        : base(new SimpleFunctionContext(null!))
    {
        // Add headers if provided
        if (headers != null)
        {
            foreach (var header in headers)
            {
                Headers.Add(header.Key, header.Value);
            }
        }

        // Initialize body stream
        _bodyStream = new MemoryStream();
        if (!string.IsNullOrEmpty(body))
        {
            var writer = new StreamWriter(_bodyStream, leaveOpen: true);
            writer.Write(body);
            writer.Flush();
            _bodyStream.Position = 0;
        }

        // Build URL
        var urlPath = string.IsNullOrEmpty(route) ? "test" : route;
        var fullUrl = $"http://localhost/{urlPath}";
        if (!string.IsNullOrEmpty(queryString))
        {
            fullUrl += queryString;
        }
        _url = new Uri(fullUrl);
    }

    /// <inheritdoc/>
    public override Stream Body => _bodyStream;

    /// <inheritdoc/>
    public override HttpHeadersCollection Headers { get; } = new HttpHeadersCollection();

    /// <inheritdoc/>
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = new List<IHttpCookie>();

    /// <inheritdoc/>
    public override Uri Url => _url;

    /// <inheritdoc/>
    public override IEnumerable<ClaimsIdentity> Identities { get; } = new List<ClaimsIdentity>();

    /// <inheritdoc/>
    public override string Method { get; } = "GET";

    /// <inheritdoc/>
    public override HttpResponseData CreateResponse() => throw new NotImplementedException();
}
