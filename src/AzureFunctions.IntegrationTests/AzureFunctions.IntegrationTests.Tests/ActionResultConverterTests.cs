using System.Net;
using AzureFunctions.IntegrationTests.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace AzureFunctions.IntegrationTests.Tests;

public class ActionResultConverterTests
{
    [Fact]
    public void ConvertToTestHttpResponse_OkObjectResult_ShouldReturnOk()
    {
        // Arrange
        var actionResult = new OkObjectResult(new { Message = "Success" });

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Success", response.Content);
    }

    [Fact]
    public void ConvertToTestHttpResponse_NotFoundObjectResult_ShouldReturnNotFound()
    {
        // Arrange
        var actionResult = new NotFoundObjectResult(new { Error = "Not found" });

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not found", response.Content);
    }

    [Fact]
    public void ConvertToTestHttpResponse_BadRequestObjectResult_ShouldReturnBadRequest()
    {
        // Arrange
        var actionResult = new BadRequestObjectResult(new { Error = "Bad request" });

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Bad request", response.Content);
    }

    [Fact]
    public void ConvertToTestHttpResponse_CreatedResult_ShouldReturnCreated()
    {
        // Arrange
        var actionResult = new CreatedResult("/users/123", new { Id = 123, Name = "New User" });

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("New User", response.Content);
    }

    [Fact]
    public void ConvertToTestHttpResponse_NoContentResult_ShouldReturnNoContent()
    {
        // Arrange
        var actionResult = new NoContentResult();

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, response.Content);
    }

    [Fact]
    public void ConvertToTestHttpResponse_UnauthorizedResult_ShouldReturnUnauthorized()
    {
        // Arrange
        var actionResult = new UnauthorizedResult();

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ConvertToTestHttpResponse_NotFoundResult_ShouldReturnNotFound()
    {
        // Arrange
        var actionResult = new NotFoundResult();

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, response.Content);
    }

    [Fact]
    public void ConvertToTestHttpResponse_ConflictObjectResult_ShouldReturnConflict()
    {
        // Arrange
        var actionResult = new ConflictObjectResult(new { Error = "Conflict" });

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Conflict", response.Content);
    }

    [Fact]
    public void ConvertToTestHttpResponse_AcceptedResult_ShouldReturnAccepted()
    {
        // Arrange
        var actionResult = new AcceptedResult("/jobs/123", new { JobId = 123 });

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public void ConvertToTestHttpResponse_ContentResult_ShouldReturnContent()
    {
        // Arrange
        var actionResult = new ContentResult
        {
            Content = "Plain text content",
            StatusCode = 200
        };

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Plain text content", response.Content);
    }

    [Fact]
    public void ConvertToTestHttpResponse_ObjectResultWithCustomStatusCode_ShouldReturnCustomCode()
    {
        // Arrange
        var actionResult = new ObjectResult(new { Message = "Custom" })
        {
            StatusCode = 418 // I'm a teapot
        };

        // Act
        var response = ActionResultConverter.ConvertToTestHttpResponse(actionResult);

        // Assert
        Assert.Equal((HttpStatusCode)418, response.StatusCode);
        Assert.Contains("Custom", response.Content);
    }
}
