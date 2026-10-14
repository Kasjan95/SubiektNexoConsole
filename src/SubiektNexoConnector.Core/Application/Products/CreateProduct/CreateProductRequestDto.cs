using System.ComponentModel.DataAnnotations;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record CreateProductRequestDto(
        [Required(AllowEmptyStrings = false)]
        [MinLength(1)]
        string Name,
        string? SKU,
        string? EAN,
        IReadOnlyCollection<AdditionalFieldValueDto>? AdvancedFields
    );
}
