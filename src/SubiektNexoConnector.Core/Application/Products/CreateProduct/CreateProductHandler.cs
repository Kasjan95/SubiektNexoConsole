namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class CreateProductHandler
    {
        private readonly IProductRepository _repository;
        private readonly IProductReadModelSynchronizer _readModelSynchronizer;

        public CreateProductHandler(IProductRepository repository, IProductReadModelSynchronizer readModelSynchronizer)
        {
            _repository = repository;
            _readModelSynchronizer = readModelSynchronizer;
        }

        public async Task<ProductDetailsDto> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
        {
            var product = _repository.Create(command);
            await _readModelSynchronizer.TryUpsertAsync(product, cancellationToken);
            return product;
        }
    }
}
