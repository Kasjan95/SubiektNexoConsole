using SubiektNexoConnector.Core.Application.Warehouses;

public interface IWarehouseRepository
{
    IReadOnlyCollection<WarehouseDto> GetAll();
}