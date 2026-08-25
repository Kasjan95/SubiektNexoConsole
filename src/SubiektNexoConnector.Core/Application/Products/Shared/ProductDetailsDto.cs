using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductDetailsDto(
        int Id,
        string SKU,
        string Name,
        string? EAN,
        ProductTypeDto Type,
        bool IsActive,
        FlagAssignmentDto? Flag,
        int? SupplierLeadTimeDays,
        IReadOnlyCollection<AdditionalFieldValueDto> BasicFields,
        IReadOnlyCollection<AdditionalFieldValueDto> AdvancedFields,
        IReadOnlyCollection<ProductSupplierDto> DefaultSuppliers,
        IReadOnlyCollection<ProductPriceDto> Prices,
        IReadOnlyCollection<ProductStockDto> Stocks
    );
}
