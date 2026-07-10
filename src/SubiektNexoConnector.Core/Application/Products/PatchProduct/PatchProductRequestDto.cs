using System.Text.Json.Serialization;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record PatchProductRequestDto(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        Optional<string> Name,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        Optional<string> SKU,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        Optional<string?> EAN
    );
}
