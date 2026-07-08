using Microsoft.OpenApi.Models;
using SubiektNexoConnector.Core.Application.Common;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SubiektNexoConnector.Api.Swagger;

public sealed class OptionalSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsGenericType || context.Type.GetGenericTypeDefinition() != typeof(Optional<>))
            return;

        var valueType = context.Type.GetGenericArguments()[0];
        var valueSchema = context.SchemaGenerator.GenerateSchema(valueType, context.SchemaRepository);

        schema.Type = valueSchema.Type;
        schema.Format = valueSchema.Format;
        schema.Nullable = valueSchema.Nullable;
        schema.Reference = valueSchema.Reference;
        schema.Properties = valueSchema.Properties;
        schema.Items = valueSchema.Items;
        schema.Required = valueSchema.Required;
        schema.AdditionalPropertiesAllowed = valueSchema.AdditionalPropertiesAllowed;
        schema.AdditionalProperties = valueSchema.AdditionalProperties;
        schema.Enum = valueSchema.Enum;
        schema.AllOf = valueSchema.AllOf;
        schema.AnyOf = valueSchema.AnyOf;
        schema.OneOf = valueSchema.OneOf;
        schema.Not = valueSchema.Not;
        schema.Description = valueSchema.Description;
        schema.Example = valueSchema.Example;
        schema.Default = valueSchema.Default;
    }
}
