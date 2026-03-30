using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.Warehouses;
using SubiektNexoConnector.Infrastructure.Abstractions;

namespace SubiektNexoConnector.Infrastructure.Nexo
{
    public class NexoWarehouseRepository : IWarehouseRepository
    {
        private readonly ISessionFactory _sessionFactory;

        public NexoWarehouseRepository(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }
        public IReadOnlyCollection<WarehouseDto> GetAll()
        {
            using var sfera = _sessionFactory.Create();

            return sfera.Magazyny()
                .Dane
                .WszystkieDostepne()
                .ToList()
                .Select(w => new WarehouseDto(w.Symbol, w.Nazwa))
                .ToArray();
        }

    }
}
