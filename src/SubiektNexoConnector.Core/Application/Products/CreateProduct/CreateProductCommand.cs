namespace SubiektNexoConnector.Core.Application.Products
{
    using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

    public sealed record CreateProductCommand(
        string Name,
        string? SKU,
        string? EAN,
        IReadOnlyCollection<AdditionalFieldValueDto> AdvancedFields
        );
}
