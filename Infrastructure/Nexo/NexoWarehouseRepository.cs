using InsERT.Moria.Sfera;
using SubiektNexoConsole.Application.Abstractions;
using SubiektNexoConsole.Application.Warehouses;
using System.Collections.Generic;
using System.Linq;

namespace SubiektNexoConsole.Infrastructure.Nexo
{
    public class NexoWarehouseRepository
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
