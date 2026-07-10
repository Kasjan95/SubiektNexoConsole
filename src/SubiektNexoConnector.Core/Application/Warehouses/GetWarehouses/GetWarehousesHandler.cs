
namespace SubiektNexoConnector.Core.Application.Warehouses
{
    public class GetWarehousesHandler
    {
        private readonly IWarehouseRepository _repository;

        public GetWarehousesHandler(IWarehouseRepository repository)
        {
            _repository = repository;
        }

        public IReadOnlyCollection<WarehouseDto> Handle(GetWarehousesQuery query)
        {
            return _repository.GetAll();
        }
    }
}
