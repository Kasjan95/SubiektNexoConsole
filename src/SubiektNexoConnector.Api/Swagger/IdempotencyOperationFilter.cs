using Microsoft.OpenApi.Models;
using SubiektNexoConnector.Api.Idempotency;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SubiektNexoConnector.Api.Swagger;

public sealed class IdempotencyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!IdempotencyPolicy.Requires(context.ApiDescription.HttpMethod ?? "",
                context.ApiDescription.ActionDescriptor))
            return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyMiddleware.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Request idempotency key.",
            Schema = new OpenApiSchema { Type = "string", MinLength = 1, MaxLength = 64, Pattern = "^[!-~]+$" }
        });
    }
}
