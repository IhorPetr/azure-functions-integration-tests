using AzureFunctions.IntegrationTests.Http;
using System.Net.Http;

namespace AzureFunctions.IntegrationTests.Http;

/// <summary>
/// HTTP client configured to make requests to the in-memory test server
/// </summary>
public class TestHttpClient : HttpClient
{
    /// <summary>
    /// Initializes a new instance of TestHttpClient with the specified factory
    /// </summary>
    /// <param name="factory">The function app factory</param>
    public TestHttpClient(dynamic factory) : base(new TestHttpMessageHandler(factory))
    {
        BaseAddress = new Uri("http://localhost/"); // this is just a dummy address, actual requests are handled by the TestHttpMessageHandler
    }
}
