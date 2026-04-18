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
        public ProductDetailsDto? GetDetails(string productSymbol)
        {
            using var sfera = _sessionFactory.Create();
            {
                var product = sfera
                    .Asortymenty()
                    .Dane
                    .WyszukajPoSymbolu(productSymbol);
                if (product is null)
                    return null;
                return new ProductDetailsDto(
                    product.Id,
                    product.Symbol,
                    product.Nazwa,
                    product.JednostkaMagazynowa?.PodstawowyKodKreskowy?.Kod,
                    new ProductTypeDto(
                        product.Rodzaj?.Symbol ?? string.Empty,
                        product.Rodzaj?.Nazwa ?? string.Empty
                    ),
                    !product.IsInRecycleBin,
                    product.LiczbaDniDoRealizacjiDostawcy,
                    MapDefaultSuppliers(product.DaneAsortymentuDostawcyPodstawowego),
                    product.PozycjeCennika.Select(c => new ProductPriceDto(
                        c.Cennik.Tytul,
                        c.CenaNetto,
                        c.CenaBrutto
                    )).ToList(),
                    product.StanyMagazynowe.Select(s => new ProductStockDto(
                        s.Magazyn.Symbol,
                        s.IloscDostepna,
                        s.IloscZadysponowana
                    )).ToList()
                );
            }
        }
        public ProductFromWarehouseDto? GetDetailsFromWarehouse(string productSymbol, string warehouseSymbol)
        {
            using var sfera = _sessionFactory.Create();
            var product = sfera
                .Asortymenty()
                .Dane
                .WyszukajPoSymbolu(productSymbol);

            if (product is null)
                return null;

            var warehouse = sfera
                .Magazyny()
                .Dane
                .WszystkieDostepne()
                .FirstOrDefault(m => m.Symbol == warehouseSymbol);

            if (warehouse is null)
                return null;

            var stockLevel = product.StanyMagazynowe.FirstOrDefault(m => m.Magazyn.Id == warehouse.Id);
            return new ProductFromWarehouseDto(
                product.Id,
                product.Symbol,
                product.Nazwa,
                product.JednostkaMagazynowa?.PodstawowyKodKreskowy?.Kod,
                warehouse.Symbol,
                stockLevel?.IloscDostepna ?? 0,
                stockLevel?.IloscZadysponowana ?? 0,
                MapStockMovement(product.Przyjecia, warehouse.Id),
                MapStockMovement(product.Wydania, warehouse.Id),
                MapStockMovement(product.Zwroty, warehouse.Id)
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

        private static IReadOnlyCollection<ProductSupplierDto> MapDefaultSuppliers(dynamic? primarySupplierData)
        {
            if (primarySupplierData is null || primarySupplierData.Podmiot is null)
                return [];

            return
            [
                new ProductSupplierDto(
                    primarySupplierData.Podmiot.Id,
                    primarySupplierData.Podmiot.NazwaSkrocona ?? primarySupplierData.Podmiot.Nazwa,
                    primarySupplierData.Podmiot.NIP,
                    true,
                    primarySupplierData.Symbol,
                    primarySupplierData.Nazwa
                )
            ];
        }

    }
}
