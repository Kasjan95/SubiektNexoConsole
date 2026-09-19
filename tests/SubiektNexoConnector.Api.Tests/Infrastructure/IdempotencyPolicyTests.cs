using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using SubiektNexoConnector.Api.Idempotency;
using SubiektNexoConnector.Api.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SubiektNexoConnector.Api.Tests.Infrastructure;

public class IdempotencyPolicyTests
{
    [Theory]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("PATCH", true)]
    [InlineData("DELETE", true)]
    [InlineData("GET", false)]
    [InlineData("HEAD", false)]
    [InlineData("OPTIONS", false)]
    public void ControllerPolicy_ProtectsOnlyWrites(string method, bool expected) =>
        Assert.Equal(expected, IdempotencyPolicy.Requires(method, new ControllerActionDescriptor()));

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void TechnicalMinimalEndpoints_AreNotProtected(string method) =>
        Assert.False(IdempotencyPolicy.Requires(method, null));

    [Theory]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("PATCH", true)]
    [InlineData("DELETE", true)]
    [InlineData("GET", false)]
    public void Swagger_UsesTheSamePolicy(string method, bool expected)
    {
        var description = new ApiDescription
        {
            HttpMethod = method,
            ActionDescriptor = new ControllerActionDescriptor()
        };
        var operation = new OpenApiOperation();
        var context = new OperationFilterContext(description, null!, new SchemaRepository(),
            typeof(IdempotencyPolicyTests).GetMethod(nameof(Swagger_UsesTheSamePolicy))!);
        new IdempotencyOperationFilter().Apply(operation, context);
        var parameter = operation.Parameters?.SingleOrDefault(p => p.Name == "Idempotency-Key");
        Assert.Equal(expected, parameter is not null);
        if (parameter is not null)
        {
            Assert.False(parameter.Required);
            Assert.Equal("Request idempotency key.", parameter.Description);
        }
    }
}
