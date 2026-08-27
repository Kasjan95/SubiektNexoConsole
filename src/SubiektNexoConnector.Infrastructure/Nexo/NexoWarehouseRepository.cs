using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.Warehouses;
using SubiektNexoConnector.Infrastructure.Abstractions;

namespace SubiektNexoConnector.Infrastructure.Nexo
{
    public class NexoWarehouseRepository : IWarehouseRepository
    {
        private readonly ISferaExecutor _sferaExecutor;

        public NexoWarehouseRepository(ISferaExecutor sferaExecutor)
        {
            _sferaExecutor = sferaExecutor;
        }
        public IReadOnlyCollection<WarehouseDto> GetAll()
        {
            return _sferaExecutor.Execute(sfera => sfera.Magazyny()
                .Dane
                .WszystkieDostepne()
                .ToList()
                .Select(w => new WarehouseDto(w.Symbol, w.Nazwa))
                .ToArray());
        }

    }
}
