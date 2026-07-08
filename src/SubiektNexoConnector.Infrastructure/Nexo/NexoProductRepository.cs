using InsERT.Moria.Asortymenty;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.EPP.Typy;
using InsERT.Moria.Sfera;
using InsERT.Mox.DataAccess.EntityFramework;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Infrastructure.Abstractions;
using System.Text;

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
        public ProductFromWarehouseDto? GetDetailsFromWarehouse(string warehouseSymbol, string productSymbol)
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
        public string Create(CreateProductCommand command)
        {
            using var sfera = _sessionFactory.Create();
            using var product = sfera.Asortymenty().Utworz();

            product.WypelnijNaPodstawieSzablonu(sfera.SzablonyAsortymentu().DaneDomyslne.Towar);
            product.Dane.Nazwa = command.Name;

            if (!string.IsNullOrWhiteSpace(command.SKU))
                product.Dane.Symbol = command.SKU;
            if (!string.IsNullOrWhiteSpace(command.EAN))
            {
                var primaryUnit = product.Dane.PodstawowaJednostkaMiaryAsortymentu;
                KodKreskowy productEan = new KodKreskowy { Kod = command.EAN.Trim() };

                primaryUnit.KodyKreskowe.Add(productEan);
                primaryUnit.PodstawowyKodKreskowy = productEan;
            }

            if (!product.Zapisz())
            {
                throw new InvalidOperationException(BuildValidationMessage(
                    "Blad dodawania produktu:",
                    product.PobierzKomunikatyBledow()));
            }

            return product.Dane.Symbol;
        }
        public string? Patch(PatchProductCommand command)
        {
            using var sfera = _sessionFactory.Create();
            var product = sfera.Asortymenty().Dane.WyszukajPoSymbolu(command.ProductSku);
            if (product is null)
                return null;

            using var productBo = sfera.Asortymenty().Znajdz(product);
            if (productBo is null)
                throw new ProductUpdateFailedException("Nie znaleziono obiektu biznesowego dla produktu.");
            var isLocked = productBo.Zablokuj();

            try
            {
                if (command.Name.HasValue)
                    productBo.Dane.Nazwa = command.Name.Value!;

                if (command.SKU.HasValue)
                    productBo.Dane.Symbol = command.SKU.Value!;

                if (command.EAN.HasValue)
                    UpdateProductEan(productBo, command.EAN.Value);

                if (!productBo.Zapisz())
                {
                    throw new InvalidOperationException(BuildValidationMessage(
                        "Blad aktualizacji produktu:",
                        productBo.PobierzKomunikatyBledow()));
                }

                return productBo.Dane.Symbol;
            }
            finally
            {
                if (isLocked)
                    productBo.Odblokuj();
            }
        }
        public DeleteProductResult Delete(DeleteProductCommand command)
        {
            using var sfera = _sessionFactory.Create();
            var product = sfera.Asortymenty().Dane.WyszukajPoSymbolu(command.SKU);
            if (product == null)
            {
                return DeleteProductResult.NotFound;
            }

            using var productBo = sfera.Asortymenty().Znajdz(product);
            if (productBo == null)
            {
                throw new ProductDeletionFailedException("Nie znaleziono obiektu biznesowego dla produktu.");
            }

            if (!productBo.MoznaUsunac)
            {
                return DeleteProductResult.Blocked;
            }

            if (!productBo.Usun())
            {
                throw new ProductDeletionFailedException("Usuwanie produktu nie powiodlo sie.");
            }

            return DeleteProductResult.Deleted;
        }

        private static void UpdateProductEan(IAsortyment productBo, string? ean)
        {
            JednostkaMiaryAsortymentu primaryUnit = productBo.Dane.PodstawowaJednostkaMiaryAsortymentu;

            if (primaryUnit is null)
                throw new ProductUpdateFailedException("Brak podstawowej jednostki miary produktu.");

            var currentPrimaryCode = primaryUnit.PodstawowyKodKreskowy;

            if (currentPrimaryCode is not null)
            {
                primaryUnit.PodstawowyKodKreskowy = null;
                primaryUnit.KodyKreskowe.Remove(currentPrimaryCode);
            }

            if (ean is null)
                return;

            KodKreskowy productEan = new KodKreskowy { Kod = ean };
            primaryUnit.KodyKreskowe.Add(productEan);
            primaryUnit.PodstawowyKodKreskowy = productEan;
        }

        private static string BuildValidationMessage(
            string messagePrefix,
            IEnumerable<KomunikatWalidacji> errors)
        {
            StringBuilder messageBuilder = new StringBuilder(messagePrefix);
            foreach (var error in errors)
            {
                var fieldNames = error.NazwyPol is null || !error.NazwyPol.Any()
                    ? "Nieznane pole"
                    : string.Join(", ", error.NazwyPol);

                messageBuilder.AppendLine();
                messageBuilder.Append($"{fieldNames}: {error.Tresc}");
            }

            return messageBuilder.ToString();
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
