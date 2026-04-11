
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed record GetProductFromWarehouseQuery(
        string ProductSymbol,
        string WarehouseSymbol
     );
}
