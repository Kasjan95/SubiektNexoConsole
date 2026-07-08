using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record PatchProductCommand(
        string ProductSku,
        Optional<string> Name,
        Optional<string> SKU,
        Optional<string?> EAN
    );
}
