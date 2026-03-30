
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class GetProductDetailsHandler
    {
        private readonly IProductRepository _repository;

        public GetProductDetailsHandler(IProductRepository repository)
        {
            _repository = repository;
        }

        public ProductDetailsDto? Handle(GetProductDetailsQuery query)
        {
            return _repository.GetDetails(query.ProductSymbol, query.WarehouseSymbol);
        }
    }
}
