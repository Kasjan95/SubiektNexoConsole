using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record ProductBasicDto(
        int Id,
        string SKU,
        string Name,
        string? EAN
    ) : ISearchable
    {
        public string GetSearchText()
        {
            return string.Join(
                ' ',
                new[]
                {
                    SKU,
                    Name,
                    EAN
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }
}
