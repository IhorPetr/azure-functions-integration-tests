using AzureFunctions.IntegrationTests.SampleApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace AzureFunctions.IntegrationTests.SampleApp.Tests;

public class CustomFactoryTests
{
    [Fact]
    public void CustomFactory_WithOverriddenConfiguration_ShouldWork()
    {
        // Arrange & Act
        using var factory = new CustomTestFactory();
        var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
        Assert.True(factory.ConfigureHostCalled);
        Assert.True(factory.ConfigureEnvironmentCalled);
    }

    [Fact]
    public void CustomFactory_CanAccessServices()
    {
        // Arrange & Act
        using var factory = new CustomTestFactory();
        var services = factory.Services;

        // Assert
        Assert.NotNull(services);
        Assert.True(factory.ConfigureHostCalled);
    }

    [Fact]
    public void CustomFactory_ConfigureEnvironment_IsInvoked()
    {
        // Arrange & Act
        using var factory = new CustomTestFactory();

        // Assert
        Assert.True(factory.ConfigureEnvironmentCalled);
        Assert.Equal("test-value", Environment.GetEnvironmentVariable("CUSTOM_TEST_VAR"));
    }

    private class CustomTestFactory : FunctionAppFactory<Program>
    {
        public bool ConfigureHostCalled { get; private set; }
        public bool ConfigureEnvironmentCalled { get; private set; }

        protected override void ConfigureHost(IHost host)
        {
            ConfigureHostCalled = true;
            base.ConfigureHost(host);
        }

        protected override void ConfigureEnvironment()
        {
            base.ConfigureEnvironment();
            ConfigureEnvironmentCalled = true;
            Environment.SetEnvironmentVariable("CUSTOM_TEST_VAR", "test-value");
        }
    }

    private interface ITestService
    {
        string GetName();
    }

    private class TestServiceImplementation : ITestService
    {
        public string GetName() => "Test Implementation";
    }
}
