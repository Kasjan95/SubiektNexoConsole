using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record PatchProductCommand(
        string ProductSku,
        Optional<string> Name,
        Optional<string> SKU,
        Optional<string?> EAN,
        Optional<IReadOnlyCollection<AdditionalFieldValueDto>> BasicFields = default,
        Optional<IReadOnlyCollection<AdditionalFieldValueDto>> AdvancedFields = default,
        Optional<FlagAssignmentDto?> Flag = default
    );
}
