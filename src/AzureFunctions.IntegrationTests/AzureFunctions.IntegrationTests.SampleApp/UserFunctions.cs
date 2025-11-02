using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json;

namespace AzureFunctions.IntegrationTests.SampleApp;

public class UserFunctions
{
    [Function("GetUsers")]
    public IActionResult GetUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users")] HttpRequestData req)
    {
        var users = new[]
        {
            new { Id = 1, Name = "John Doe", Email = "john@example.com" },
            new { Id = 2, Name = "Jane Smith", Email = "jane@example.com" }
        };

        return new OkObjectResult(users);
    }

    [Function("GetUserById")]
    public IActionResult GetUserById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{userId}")] HttpRequestData req,
        int userId)
    {
        if (userId <= 0)
        {
            return new BadRequestObjectResult(new { error = "Invalid user ID" });
        }

        if (userId > 100)
        {
            return new NotFoundObjectResult(new { error = "User not found" });
        }

        var user = new { Id = userId, Name = $"User {userId}", Email = $"user{userId}@example.com" };
        return new OkObjectResult(user);
    }

    [Function("GetUserByGuid")]
    public IActionResult GetUserByGuid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/guid/{userGuid}")] HttpRequestData req,
        Guid userGuid)
    {
        var user = new { Id = userGuid, Name = $"User {userGuid}" };
        return new OkObjectResult(user);
    }

    [Function("CreateUser")]
    public async Task<IActionResult> CreateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users")] HttpRequestData req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return new BadRequestObjectResult(new { error = "Request body is required" });
            }

            var user = JsonSerializer.Deserialize<CreateUserRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (user == null || string.IsNullOrWhiteSpace(user.Name))
            {
                return new BadRequestObjectResult(new { error = "Name is required" });
            }

            var createdUser = new { Id = 999, user.Name, user.Email };
            return new CreatedResult($"/users/999", createdUser);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }
    }

    [Function("UpdateUser")]
    public async Task<IActionResult> UpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "users/{userId}")] HttpRequestData req,
        int userId)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var user = JsonSerializer.Deserialize<CreateUserRequest>(requestBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (user == null)
        {
            return new BadRequestObjectResult(new { error = "Invalid user data" });
        }

        var updatedUser = new { Id = userId, user.Name, user.Email };
        return new OkObjectResult(updatedUser);
    }

    [Function("DeleteUser")]
    public IActionResult DeleteUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "users/{userId}")] HttpRequestData req,
        int userId)
    {
        if (userId <= 0)
        {
            return new BadRequestObjectResult(new { error = "Invalid user ID" });
        }

        return new NoContentResult();
    }

    [Function("GetUserWithHeaders")]
    public IActionResult GetUserWithHeaders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/withheaders")] HttpRequestData req)
    {
        var tenantId = req.Headers.TryGetValues("X-Tenant-Id", out var tenantValues)
            ? tenantValues.FirstOrDefault()
            : null;

        if (string.IsNullOrEmpty(tenantId))
        {
            return new UnauthorizedResult();
        }

        var user = new { Id = 1, Name = "John Doe", TenantId = tenantId };
        return new OkObjectResult(user);
    }
}

public record CreateUserRequest(string Name, string? Email);
