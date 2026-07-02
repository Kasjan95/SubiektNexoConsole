namespace SubiektNexoConnector.Core.Application.Products
{
    public sealed class CreateProductHandler
    {
        private readonly IProductRepository _repository;

        public CreateProductHandler(IProductRepository repository)
        {
            _repository = repository;
        }
        public string Handle(CreateProductCommand command)
        {
            return _repository.Create(command);
        }
    }
}
