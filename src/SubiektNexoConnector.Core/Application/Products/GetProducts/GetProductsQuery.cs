
namespace SubiektNexoConnector.Core.Application.Products
{
   public sealed class GetProductsQuery
    {
        public string? Search { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 100;
    }
}
