using System.Text.Json;
using AzureFunctions.IntegrationTests.Http;
using Microsoft.AspNetCore.Mvc;

namespace AzureFunctions.IntegrationTests.Extensions;

/// <summary>
/// Converts IActionResult instances to test HTTP responses
/// </summary>
public static class ActionResultConverter
{
    /// <summary>
    /// Converts an IActionResult to a TestHttpResponse
    /// </summary>
    /// <param name="actionResult">The action result to convert</param>
    /// <returns>A TestHttpResponse with the appropriate status code and content</returns>
    public static TestHttpResponse ConvertToTestHttpResponse(IActionResult actionResult)
    {
        var response = new TestHttpResponse();

        switch (actionResult)
        {
            // Handle specific ObjectResult types first (before generic ObjectResult)
            case OkObjectResult okResult:
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content = JsonSerializer.Serialize(okResult.Value);
                break;

            case CreatedResult createdResult:
                response.StatusCode = System.Net.HttpStatusCode.Created;
                response.Content = JsonSerializer.Serialize(createdResult.Value);
                break;

            case CreatedAtActionResult createdAtActionResult:
                response.StatusCode = System.Net.HttpStatusCode.Created;
                response.Content = JsonSerializer.Serialize(createdAtActionResult.Value);
                break;

            case NotFoundObjectResult notFoundResult:
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
                response.Content = JsonSerializer.Serialize(notFoundResult.Value);
                break;

            case BadRequestObjectResult badRequestResult:
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                response.Content = JsonSerializer.Serialize(badRequestResult.Value);
                break;

            case ConflictObjectResult conflictResult:
                response.StatusCode = System.Net.HttpStatusCode.Conflict;
                response.Content = JsonSerializer.Serialize(conflictResult.Value);
                break;

            case UnprocessableEntityObjectResult unprocessableResult:
                response.StatusCode = System.Net.HttpStatusCode.UnprocessableEntity;
                response.Content = JsonSerializer.Serialize(unprocessableResult.Value);
                break;

            case AcceptedResult acceptedResult:
                response.StatusCode = System.Net.HttpStatusCode.Accepted;
                response.Content = acceptedResult.Value != null ? JsonSerializer.Serialize(acceptedResult.Value) : string.Empty;
                break;

            // Generic ObjectResult case (must come after all specific ObjectResult types)
            case ObjectResult objectResult:
                response.StatusCode = (System.Net.HttpStatusCode)(objectResult.StatusCode ?? 200);
                response.Content = objectResult.Value != null ? JsonSerializer.Serialize(objectResult.Value) : string.Empty;
                break;

            // Handle other result types (not derived from ObjectResult)


            case ContentResult contentResult:
                response.StatusCode = (System.Net.HttpStatusCode)(contentResult.StatusCode ?? 200);
                response.Content = contentResult.Content ?? string.Empty;
                break;

            case NotFoundResult:
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
                response.Content = string.Empty;
                break;

            case NoContentResult:
                response.StatusCode = System.Net.HttpStatusCode.NoContent;
                response.Content = string.Empty;
                break;

            case UnauthorizedResult:
                response.StatusCode = System.Net.HttpStatusCode.Unauthorized;
                response.Content = string.Empty;
                break;

            default:
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                response.Content = JsonSerializer.Serialize(new { error = $"Unsupported action result type: {actionResult.GetType().Name}" });
                break;
        }

        return response;
    }
}
