
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record GetProductFromWarehouseQuery(
        string WarehouseSymbol,
        string ProductSymbol
     );
}
