using SubiektNexoConsole.Infrastructure.Nexo;
using System;
using System.Collections.Generic;

namespace SubiektNexoConsole.Application.Warehouses
{
    public class GetWarehousesHandler
    {
        private readonly NexoWarehouseRepository _repository;

        public GetWarehousesHandler(NexoWarehouseRepository repository)
        {
            _repository = repository;
        }

        public IReadOnlyCollection<WarehouseDto> Handle(GetWarehousesQuery query)
        {
            return _repository.GetAll();
        }
    }
}
