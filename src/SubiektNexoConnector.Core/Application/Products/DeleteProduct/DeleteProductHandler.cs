namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class DeleteProductHandler
    {
        private readonly IProductRepository _repository;
        public DeleteProductHandler(IProductRepository repository)
        {
            _repository = repository;
        }
        public DeleteProductResult Handle(DeleteProductCommand command)
        {
            return _repository.Delete(command);
        }
    }
}
