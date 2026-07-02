using System.ComponentModel.DataAnnotations;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record CreateProductRequestDto(
        [Required(AllowEmptyStrings = false)]
        [MinLength(1)]
        string Name,
        string? SKU,
        string? EAN
    );
}
