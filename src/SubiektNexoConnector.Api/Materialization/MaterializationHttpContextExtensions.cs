using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Api.Materialization;

public static class MaterializationHttpContextExtensions
{
    public static void SetMaterializationMetadata(this HttpContext context, MaterializationMetadata metadata) => MaterializationMetadataMiddleware.SetMetadata(context, metadata);
}
