namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class DeleteProductHandler
    {
        private readonly IProductRepository _repository;
        private readonly IProductReadModelSynchronizer _readModelSynchronizer;

        public DeleteProductHandler(IProductRepository repository, IProductReadModelSynchronizer readModelSynchronizer)
        {
            _repository = repository;
            _readModelSynchronizer = readModelSynchronizer;
        }

        public async Task<DeleteProductResult> HandleAsync(DeleteProductCommand command, CancellationToken cancellationToken = default)
        {
            var result = _repository.Delete(command);

            if (result is DeleteProductResult.Deleted or DeleteProductResult.NotFound)
                await _readModelSynchronizer.TryDeleteAsync(command.SKU, cancellationToken);

            return result;
        }
    }
}
