using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureFunctions.IntegrationTests.SampleApp.Tests;

public class UserApiIntegrationTests : IClassFixture<FunctionAppFactory<Program>>
{
    private readonly FunctionAppFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserApiIntegrationTests(FunctionAppFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUsers_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.Contains("John Doe", content);
    }

    [Fact]
    public async Task GetUserById_WithValidId_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/users/42");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<dynamic>(content);
        Assert.NotNull(user);
    }

    [Fact]
    public async Task GetUserById_WithInvalidId_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/users/0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/users/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUserByGuid_WithValidGuid_ShouldReturnOk()
    {
        // Arrange
        var userGuid = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/users/guid/{userGuid}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(userGuid.ToString(), content);
    }

    [Fact]
    public async Task CreateUser_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var newUser = new { Name = "Test User", Email = "test@example.com" };
        var content = new StringContent(
            JsonSerializer.Serialize(newUser),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/users", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test User", responseContent);
    }

    [Fact]
    public async Task CreateUser_WithEmptyBody_ShouldReturnBadRequest()
    {
        // Arrange
        var content = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/users", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithInvalidJson_ShouldReturnBadRequest()
    {
        // Arrange
        var content = new StringContent("{ invalid json", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/users", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_WithValidData_ShouldReturnOk()
    {
        // Arrange
        var updatedUser = new { Name = "Updated User", Email = "updated@example.com" };
        var content = new StringContent(
            JsonSerializer.Serialize(updatedUser),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync("/api/users/42", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithValidId_ShouldReturnNoContent()
    {
        // Act
        var response = await _client.DeleteAsync("/api/users/42");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithInvalidId_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.DeleteAsync("/api/users/0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUserWithHeaders_WithoutTenantId_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/users/withheaders");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserWithHeaders_WithTenantId_ShouldReturnOk()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "test-tenant-123");

        // Act
        var response = await _client.GetAsync("/api/users/withheaders");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("test-tenant-123", content);
    }

    [Fact]
    public async Task NonExistentRoute_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WrongHttpMethod_ShouldReturnMethodNotAllowed()
    {
        // Act - GET users doesn't support DELETE
        var response = await _client.DeleteAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
