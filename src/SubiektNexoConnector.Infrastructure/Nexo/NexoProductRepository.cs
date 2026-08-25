using InsERT.Moria.Asortymenty;
using InsERT.Moria.Flagi;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Narzedzia.EPP.Typy;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Rozszerzanie;
using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Products;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Nexo.Common;

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
                IProstePolaWlasne prostePolaWlasne = sfera.PodajObiektTypu<IProstePolaWlasne>();
                var product = sfera.Asortymenty().Dane.WyszukajPoSymbolu(productSymbol);
                if (product is null)
                    return null;

                var asortymentBo = sfera.Asortymenty().Znajdz(product);
                if (asortymentBo is null)
                    return null;

                var asoPWAccessor = sfera.UtworzPolaWlasneStdAccessor(asortymentBo.Dane);
                var advancedFields = sfera.PodajObiektTypu<IZaawansowanePolaWlasne>();
                var hasAdvancedFields = advancedFields.SprobujPobracZaawansowanePolaWlasne(
                    typeof(InsERT.Moria.ModelDanych.Asortyment),
                    out var advancedFieldDefinitions);
                var asoPW2Accessor = hasAdvancedFields
                    ? sfera.UtworzPolaWlasneAdv2Accessor(
                        asortymentBo.Dane,
                        PolaWlasneAdv2AccessorFactoryNullHandlingKind.CreateReadonlyStub)
                    : null;

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
                    product.FlagaWlasna is null
                        ? null
                        : new FlagAssignmentDto(
                            product.FlagaWlasna.Id,
                            product.FlagHeader?.Description),
                    product.LiczbaDniDoRealizacjiDostawcy,
                    NexoAdditionalFieldValueMapper.MapBasic(
                        prostePolaWlasne,
                        asoPWAccessor,
                        typeof(InsERT.Moria.ModelDanych.Asortyment)),
                    asoPW2Accessor is null
                        ? []
                        : NexoAdditionalFieldValueMapper.MapAdvanced(
                            advancedFieldDefinitions,
                            asoPW2Accessor),
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
        public IReadOnlyCollection<ProductBasicDto> GetAll(GetProductsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            using var sfera = _sessionFactory.Create();

            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 1000);

            var products = sfera
                .Asortymenty()
                .Dane
                .Wszystkie()
                .OrderBy(a => a.Id)
                .ToList()
                .Select(
                a => new ProductBasicDto(
                        a.Id,
                        a.Symbol,
                        a.Nazwa,
                        a.JednostkaMagazynowa?.PodstawowyKodKreskowy?.Kod ?? string.Empty
                 ));

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchTerm = query.Search.Trim();
                products = products.Where(a =>
                    SearchTextMatcher.MatchesSearch(a, searchTerm));
            }

            return products
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
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

            if (IsFlagOnlyPatch(command))
            {
                UpdateProductFlag(sfera, product.Id, command.Flag.Value);
                return product.Symbol;
            }

            using var productBo = sfera.Asortymenty().Znajdz(product);
            if (productBo is null)
                throw new ProductUpdateFailedException("Nie znaleziono obiektu biznesowego dla produktu.");
            var isLocked = productBo.Zablokuj();

            string updatedSku;

            try
            {
                if (command.Name.HasValue)
                    productBo.Dane.Nazwa = command.Name.Value!;

                if (command.SKU.HasValue)
                    productBo.Dane.Symbol = command.SKU.Value!;

                if (command.EAN.HasValue)
                    UpdateProductEan(productBo, command.EAN.Value);

                if (command.BasicFields.HasValue)
                {
                    var basicFields = sfera.PodajObiektTypu<IProstePolaWlasne>();
                    var basicValues = sfera.UtworzPolaWlasneStdAccessor(productBo.Dane);

                    NexoAdditionalFieldValueMapper.ApplyBasic(
                        basicFields,
                        basicValues,
                        typeof(InsERT.Moria.ModelDanych.Asortyment),
                        command.BasicFields.Value!);
                }

                if (command.AdvancedFields.HasValue)
                {
                    var advancedFields = sfera.PodajObiektTypu<IZaawansowanePolaWlasne>();
                    if (!advancedFields.SprobujPobracZaawansowanePolaWlasne(
                        typeof(InsERT.Moria.ModelDanych.Asortyment),
                        out var advancedDefinitions))
                    {
                        throw new InvalidOperationException("Product does not support advanced fields.");
                    }

                    var advancedValues = sfera.UtworzPolaWlasneAdv2Accessor(
                        productBo.Dane,
                        PolaWlasneAdv2AccessorFactoryNullHandlingKind.CreateAndAttach);

                    NexoAdditionalFieldValueMapper.ApplyAdvanced(
                        advancedDefinitions,
                        advancedValues,
                        command.AdvancedFields.Value!);
                }

                if (!productBo.Zapisz())
                {
                    throw new InvalidOperationException(NexoValidationMessageBuilder.Build(
                        "Blad aktualizacji produktu:",
                        productBo.PobierzKomunikatyBledow()));
                }

                updatedSku = productBo.Dane.Symbol;
            }
            finally
            {
                if (isLocked)
                    productBo.Odblokuj();
            }

            if (command.Flag.HasValue)
                UpdateProductFlag(sfera, product.Id, command.Flag.Value);

            return updatedSku;
        }

        private static void UpdateProductFlag(
            Uchwyt sfera,
            int productId,
            FlagAssignmentDto? flag)
        {
            var flags = sfera.PodajObiektTypu<IFlagiWlasne>();
            var result = flag is null
                ? flags.UsunFlage(typeof(InsERT.Moria.ModelDanych.Asortyment), productId)
                : flags.NadajFlage(
                    flag.Id,
                    flag.Comment,
                    typeof(InsERT.Moria.ModelDanych.Asortyment),
                    productId);

            if (!result)
                throw new InvalidOperationException(
                    $"Product flag update failed for product id {productId} and flag id {flag?.Id}: {result}");
        }

        private static bool IsFlagOnlyPatch(PatchProductCommand command) =>
            command.Flag.HasValue
            && !command.Name.HasValue
            && !command.SKU.HasValue
            && !command.EAN.HasValue
            && !command.BasicFields.HasValue
            && !command.AdvancedFields.HasValue;

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
