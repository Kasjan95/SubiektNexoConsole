
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class GetProductFromWarehouseHandler
    {
        private readonly IProductRepository _repository;

        public GetProductFromWarehouseHandler(IProductRepository repository)
        {
            _repository = repository;
        }

        public ProductFromWarehouseDto? Handle(GetProductFromWarehouseQuery query)
        {
            return _repository.GetDetailsFromWarehouse(query.WarehouseSymbol, query.ProductSymbol);
        }
    }
}
