
namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class GetProductsHandler
    {
        private readonly IProductRepository _repository;

        public GetProductsHandler(IProductRepository repository)
        {
            _repository = repository;
        }

        public IReadOnlyCollection<ProductBasicDto> Handle(GetProductsQuery query)
        {
            return _repository.GetAll();
        }
    }
}
