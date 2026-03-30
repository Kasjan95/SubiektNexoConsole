
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record GetProductDetailsQuery(
        string ProductSymbol,
        string WarehouseSymbol
     );
}
