using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using InsERT.Mox.DataAccess.EntityFramework;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Infrastructure.Abstractions;

namespace SubiektNexoConnector.Infrastructure.Nexo
{
    public class NexoProductRepository : IProductRepository
    {
        private readonly ISessionFactory _sessionFactory;
        public NexoProductRepository(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }
        public ProductDetailsDto? GetDetails(string productSymbol, string warehouseSymbol)
        {
            using var sfera = _sessionFactory.Create();
            var asortyment = sfera
                .Asortymenty()
                .Dane
                .WyszukajPoSymbolu(productSymbol);

            if (asortyment is null)
                return null;

            var magazyn = sfera
                .Magazyny()
                .Dane
                .WszystkieDostepne()
                .FirstOrDefault(m => m.Symbol == warehouseSymbol);

            if (magazyn is null)
                return null;

            var stan = asortyment.StanyMagazynowe.Where(m => m.Id == magazyn.Id).FirstOrDefault();
            return new ProductDetailsDto(
                asortyment.Id,
                asortyment.Symbol,
                asortyment.Nazwa,
                asortyment.JednostkaMagazynowa?.PodstawowyKodKreskowy?.Kod,
                magazyn.Symbol,
                stan?.IloscDostepna ?? 0,
                stan?.IloscZadysponowana ?? 0,
                MapStockMovement(asortyment.Przyjecia, magazyn.Id),
                MapStockMovement(asortyment.Wydania, magazyn.Id),
                MapStockMovement(asortyment.Zwroty, magazyn.Id)
            );
        }
        public IReadOnlyCollection<ProductBasicDto> GetAll()
        {
            using var sfera = _sessionFactory.Create();

            return sfera
                .Asortymenty()
                .Dane
                .WszystkieDostepne()
                .ToList()
                .Select(
                a => new ProductBasicDto(
                        a.Id,
                        a.Symbol,
                        a.Nazwa,
                        a.JednostkaMagazynowa?.PodstawowyKodKreskowy?.Kod ?? string.Empty
                 ))
                .ToList();
        }

        private static StockMovementDto MapStockMovement(IEnumerable<dynamic> movements, int warehouseId)
        {
            var items = movements
                .Where(x =>
                    x?.PozycjaDokumentu?.Dokument?.Magazyn?.Id == warehouseId)
                .Select(x => new StockOperationDto(
                    x.PozycjaDokumentu?.Dokument?.NumerWewnetrzny?.PelnaSygnatura ?? string.Empty,
                    x.PozycjaDokumentu?.Dokument?.DataWprowadzenia ?? DateTime.MinValue,
                    x.Ilosc,
                    x.PozycjaDokumentu?.Dokument?.NumerWewnetrzny?.SygnaturaPrzedNr ?? string.Empty
                ))
                .ToList();

            return new StockMovementDto(
                items.Count,
                items.Sum(x => x.Quantity),
                items
            );
        }
    }
}
