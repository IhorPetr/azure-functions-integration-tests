using System.Net;
using System.Text;
using System.Text.Json;
using AzureFunctions.IntegrationTests.SampleApp;


namespace AzureFunctions.IntegrationTests.SampleApp.Tests;

public class FunctionAppFactoryTests : IClassFixture<FunctionAppFactory<Program>>
{
    private readonly FunctionAppFactory<Program> _factory;
    private readonly HttpClient _client;

    public FunctionAppFactoryTests(FunctionAppFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void Factory_ShouldProvideServices()
    {
        // Assert
        Assert.NotNull(_factory.Services);
    }

    [Fact]
    public void CreateClient_ShouldReturnHttpClient()
    {
        // Act
        var client = _factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.NotNull(client.BaseAddress);
    }

    [Fact]
    public void CreateClient_WithCustomBaseAddress_ShouldSetBaseAddress()
    {
        // Arrange
        var customUri = new Uri("http://custom-host/");

        // Act
        var client = _factory.CreateClient(customUri);

        // Assert
        Assert.Equal(customUri, client.BaseAddress);
    }

    [Fact]
    public void Factory_ShouldCreateServiceBusDispatcher()
    {
        var dispatcher = _factory.CreateAzureServiceBusFunctionInvoker();

        Assert.NotNull(dispatcher);
    }
}
