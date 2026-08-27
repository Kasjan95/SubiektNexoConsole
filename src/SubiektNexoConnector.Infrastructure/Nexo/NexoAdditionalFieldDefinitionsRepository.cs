using System.Globalization;
using InsERT.Moria.Flagi;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetBasicFieldDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFlagDefinitions;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Infrastructure.Abstractions;

namespace SubiektNexoConnector.Infrastructure.Nexo
{
    public sealed class NexoAdditionalFieldDefinitionsRepository : IAdditionalFieldDefinitionRepository
    {
        private readonly ISferaExecutor _sferaExecutor;

        public NexoAdditionalFieldDefinitionsRepository(ISferaExecutor sferaExecutor)
        {
            _sferaExecutor = sferaExecutor;
        }

        public AdvancedFieldDefinitionsDto GetAdvancedFieldDefinitions(GetAdvancedFieldDefinitionsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return _sferaExecutor.Execute(sfera =>
            {
            var fields = sfera.PodajObiektTypu<IZaawansowanePolaWlasne>();
            var entityType = GetEntityType(query.Target);

            if (!fields.SprobujPobracZaawansowanePolaWlasne(entityType, out var definitions))
                return new AdvancedFieldDefinitionsDto(
                    query.Target,
                    Array.Empty<AdvancedFieldGroupDto>(),
                    Array.Empty<AdvancedFieldDefinitionDto>());

            var mappedFields = definitions
                .Select(field => new FieldWithGroup(
                    field.Grupa?.Nazwa,
                    field.Grupa?.PozycjaWyswietlania,
                    field.PozycjaWyswietlania,
                    MapDefinition(field)))
                .ToList();

            var groups = mappedFields
                .Where(field => field.GroupName is not null)
                .GroupBy(field => new { field.GroupName, field.GroupPosition })
                .OrderBy(group => group.Key.GroupPosition)
                .ThenBy(group => group.Key.GroupName)
                .Select(group => new AdvancedFieldGroupDto(
                    group.Key.GroupName!,
                    group.Key.GroupPosition ?? 0,
                    group.OrderBy(field => field.Position)
                        .Select(field => field.Definition)
                        .ToList()))
                .ToList();

            var ungroupedFields = mappedFields
                .Where(field => field.GroupName is null)
                .OrderBy(field => field.Position)
                .Select(field => field.Definition)
                .ToList();

                return new AdvancedFieldDefinitionsDto(query.Target, groups, ungroupedFields);
            });
        }

        public BasicFieldDefinitionsDto GetBasicFieldDefinitions(GetBasicFieldDefinitionsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return _sferaExecutor.Execute(sfera =>
            {
            var fields = sfera.PodajObiektTypu<IProstePolaWlasne>();
            var entityType = GetEntityType(query.Target);

            var definitions = fields.MaProstePolaWlasne(entityType)
                ? fields
                .PobierzProstePolaWlasne(entityType)
                .Select(field => new BasicFieldDefinitionDto(
                    field.Id,
                    field.Nazwa,
                    field.Widoczne))
                .ToList()
                : [];

                return new BasicFieldDefinitionsDto(query.Target, definitions);
            });
        }

        public FlagDefinitionsDto GetFlagDefinitions(GetFlagDefinitionQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return _sferaExecutor.Execute(sfera =>
            {
            var flags = sfera.FlagiWlasne()
                .Dane
                .Wszystkie()
                .AsEnumerable();

            if (query.Domain.HasValue)
            {
                flags = query.Domain.Value is int domainId
                    ? flags.Where(flag => flag.Domena == domainId)
                    : flags.Where(flag => flag.Domena is null);
            }

            var domains = flags
                .GroupBy(flag => flag.Domena)
                .OrderBy(group => group.Key is not null)
                .ThenBy(group => group.Key)
                .Select(group => new FlagDomainDto(
                    group.Key,
                    group.Key is byte domainId ? GetDomainName(domainId) : null,
                    group
                        .OrderBy(flag => flag.Nazwa)
                        .Select(MapFlagDefinition)
                        .ToList()))
                .ToList();

                return new FlagDefinitionsDto(domains);
            });
        }
 
        #region Target mapping

        private static Type GetEntityType(AdditionalFieldTarget target) => target switch
        {
            AdditionalFieldTarget.Product => typeof(Asortyment),
            AdditionalFieldTarget.Party => typeof(Podmiot),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };

        #endregion

        #region Flag mapping

        private static FlagDefinitionDto MapFlagDefinition(FlagaWlasna flag) => new(
            flag.Id,
            flag.Nazwa,
            flag.Opis,
            flag.Kolor,
            GetShapeName(flag.Ksztalt),
            flag.SzybkaFlaga,
            flag.ZawszeWidoczna);

        private static string? GetDomainName(byte domainId) =>
            Enum.GetName(typeof(DomenaFlagiWlasnej), (DomenaFlagiWlasnej)domainId);

        private static string GetShapeName(byte shapeId) =>
            Enum.GetName(typeof(KsztaltFlagiWlasnej), (KsztaltFlagiWlasnej)shapeId)
            ?? shapeId.ToString(CultureInfo.InvariantCulture);

        #endregion

        #region Advanced field mapping

        private static AdvancedFieldDefinitionDto MapDefinition(IZaawansowanePoleWlasne field)
        {
            var dictionary = field.JestReferencjaDoSlownika
                ? MapDictionary(field.PobierzDefinicjeSlownika())
                : null;

            return new AdvancedFieldDefinitionDto(
                field.Id,
                field.Nazwa,
                field.Opis,
                dictionary is null ? MapDataType(field.Typ) : AdvancedFieldDataType.Dictionary,
                field.Wymagane,
                field.Widoczne,
                field.Edytowalne,
                field.Klonowalne,
                field.Precyzja,
                field.MinWidoczneLinie,
                field.MaxWidoczneLinie,
                field.WartoscDomyslna,
                dictionary);
        }

        private static AdvancedFieldDataType MapDataType(TypSkalarnyZaawansowanegoPolaWlasnego type) => type switch
        {
            TypSkalarnyZaawansowanegoPolaWlasnego.Tekst => AdvancedFieldDataType.Text,
            TypSkalarnyZaawansowanegoPolaWlasnego.DlugiTekst => AdvancedFieldDataType.LongText,
            TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita => AdvancedFieldDataType.Integer,
            TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaRzeczywista => AdvancedFieldDataType.Decimal,
            TypSkalarnyZaawansowanegoPolaWlasnego.WartoscLogiczna => AdvancedFieldDataType.Boolean,
            TypSkalarnyZaawansowanegoPolaWlasnego.Data => AdvancedFieldDataType.Date,
            TypSkalarnyZaawansowanegoPolaWlasnego.Guid => AdvancedFieldDataType.Guid,
            _ => AdvancedFieldDataType.Unknown
        };

        private sealed record FieldWithGroup(
            string? GroupName,
            int? GroupPosition,
            int Position,
            AdvancedFieldDefinitionDto Definition);

        #endregion

        #region Dictionary mapping

        private static AdvancedFieldDictionaryDto MapDictionary(ISlownikoweZrodloDanych dictionary) => dictionary.Rodzaj switch
        {
            RodzajSlownikowegoZrodlaDanych.SlownikWlasny => MapCustomDictionary(
                (ISlownikoweZrodloDanych<int, PozycjaSlownikaWlasnego>)dictionary),
            RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql => MapCustomSqlDictionary(dictionary),
            RodzajSlownikowegoZrodlaDanych.SlownikSystemowy => MapSystemDictionary(dictionary),
            _ => throw new NotSupportedException($"Unsupported dictionary kind: {dictionary.Rodzaj}.")
        };

        #region Custom dictionaries

        private static AdvancedFieldDictionaryDto MapCustomDictionary(
            ISlownikoweZrodloDanych<int, PozycjaSlownikaWlasnego> dictionary) => new(
                AdvancedFieldDictionaryKind.Custom,
                "int",
                null,
                dictionary.UtworzZapytanieFiltrowaneLinqTypowane()
                    .AsEnumerable()
                    .Select(item => new AdvancedFieldDictionaryOptionDto(
                        item.Id.ToString(CultureInfo.InvariantCulture),
                        item.Wartosc,
                        item.Aktywna))
                    .ToList());

        private static AdvancedFieldDictionaryDto MapCustomSqlDictionary(ISlownikoweZrodloDanych dictionary)
        {
            if (dictionary.TypKlucza == typeof(int))
            {
                var typedDictionary = (ISlownikoweZrodloDanych<int>)dictionary;
                return new AdvancedFieldDictionaryDto(
                    AdvancedFieldDictionaryKind.CustomSql,
                    "int",
                    null,
                    typedDictionary.UtworzZapytanieFiltrowaneLinq()
                        .AsEnumerable()
                        .Select(item => new AdvancedFieldDictionaryOptionDto(
                            item.Klucz.ToString(CultureInfo.InvariantCulture),
                            item.Wartosc,
                            null))
                        .ToList());
            }

            if (dictionary.TypKlucza == typeof(Guid))
            {
                var typedDictionary = (ISlownikoweZrodloDanych<Guid>)dictionary;
                return new AdvancedFieldDictionaryDto(
                    AdvancedFieldDictionaryKind.CustomSql,
                    "guid",
                    null,
                    typedDictionary.UtworzZapytanieFiltrowaneLinq()
                        .AsEnumerable()
                        .Select(item => new AdvancedFieldDictionaryOptionDto(
                            item.Klucz.ToString(),
                            item.Wartosc,
                            null))
                        .ToList());
            }

            return new AdvancedFieldDictionaryDto(
                AdvancedFieldDictionaryKind.CustomSql,
                dictionary.TypKlucza.FullName ?? dictionary.TypKlucza.Name,
                null,
                null);
        }

        #endregion

        #region System dictionaries

        private static AdvancedFieldDictionaryDto MapSystemDictionary(ISlownikoweZrodloDanych dictionary) => dictionary.Id switch
        {
            IdentyfikatorSlownikaSystemowego.Waluty => MapCurrencies(
                (ISlownikoweZrodloDanych<Guid, Waluta>)dictionary),
            IdentyfikatorSlownikaSystemowego.Magazyny => MapWarehouses(
                (ISlownikoweZrodloDanych<int, Magazyn>)dictionary),
            IdentyfikatorSlownikaSystemowego.RachunkiBankowe => MapBankAccounts(
                (ISlownikoweZrodloDanych<int, RachunekBankowy>)dictionary),
            _ => new AdvancedFieldDictionaryDto(
                AdvancedFieldDictionaryKind.System,
                dictionary.TypKlucza.FullName ?? dictionary.TypKlucza.Name,
                dictionary.Id.ToString(),
                null)
        };

        private static AdvancedFieldDictionaryDto MapCurrencies(
            ISlownikoweZrodloDanych<Guid, Waluta> dictionary) => new(
                AdvancedFieldDictionaryKind.System,
                "guid",
                "currencies",
                dictionary.UtworzZapytanieFiltrowaneLinqTypowane()
                    .AsEnumerable()
                    .Select(item => new AdvancedFieldDictionaryOptionDto(
                        item.Id.ToString(),
                        $"{item.Symbol} | {item.Nazwa}",
                        null))
                    .ToList());

        private static AdvancedFieldDictionaryDto MapWarehouses(
            ISlownikoweZrodloDanych<int, Magazyn> dictionary) => new(
                AdvancedFieldDictionaryKind.System,
                "int",
                "warehouses",
                dictionary.UtworzZapytanieFiltrowaneLinqTypowane()
                    .AsEnumerable()
                    .Select(item => new AdvancedFieldDictionaryOptionDto(
                        item.Id.ToString(CultureInfo.InvariantCulture),
                        $"{item.Symbol} | {item.Nazwa}",
                        null))
                    .ToList());

        private static AdvancedFieldDictionaryDto MapBankAccounts(
            ISlownikoweZrodloDanych<int, RachunekBankowy> dictionary) => new(
                AdvancedFieldDictionaryKind.System,
                "int",
                "bank-accounts",
                dictionary.UtworzZapytanieFiltrowaneLinqTypowane()
                    .AsEnumerable()
                    .Select(item => new AdvancedFieldDictionaryOptionDto(
                        item.Id.ToString(CultureInfo.InvariantCulture),
                        $"{item.Nazwa} | {item.Numer}",
                        null))
                    .ToList());

        #endregion

        #endregion
    }
}
