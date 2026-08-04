using System.Globalization;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.PolaWlasne2;
using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.AdditionalFields.GetFieldsType;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Infrastructure.Abstractions;

namespace SubiektNexoConnector.Infrastructure.Nexo
{
    public sealed class NexoAdditionalFieldsRepository : IAdditionalFieldRepository
    {
        private readonly ISessionFactory _sessionFactory;

        public NexoAdditionalFieldsRepository(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        public AdditionalFieldsDefinitionDto GetFieldsType(GetFieldsTypeQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            using Uchwyt sfera = _sessionFactory.Create();
            var fields = sfera.PodajObiektTypu<IZaawansowanePolaWlasne>();
            var entityType = GetEntityType(query.Target);

            if (!fields.SprobujPobracZaawansowanePolaWlasne(entityType, out var definitions))
                return new AdditionalFieldsDefinitionDto(
                    query.Target,
                    Array.Empty<AdditionalFieldGroupDto>(),
                    Array.Empty<AdditionalFieldDefinitionDto>());

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
                .Select(group => new AdditionalFieldGroupDto(
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

            return new AdditionalFieldsDefinitionDto(query.Target, groups, ungroupedFields);
        }

        private static Type GetEntityType(AdditionalFieldTarget target) => target switch
        {
            AdditionalFieldTarget.Product => typeof(Asortyment),
            AdditionalFieldTarget.Party => typeof(Podmiot),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };

        private static AdditionalFieldDefinitionDto MapDefinition(IZaawansowanePoleWlasne field)
        {
            var dictionary = field.JestReferencjaDoSlownika
                ? MapDictionary(field.PobierzDefinicjeSlownika())
                : null;

            return new AdditionalFieldDefinitionDto(
                field.Id,
                field.Nazwa,
                field.Opis,
                dictionary is null ? MapDataType(field.Typ) : AdditionalFieldDataType.Dictionary,
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

        private sealed record FieldWithGroup(
            string? GroupName,
            int? GroupPosition,
            int Position,
            AdditionalFieldDefinitionDto Definition);

        private static AdditionalFieldDataType MapDataType(TypSkalarnyZaawansowanegoPolaWlasnego type) => type switch
        {
            TypSkalarnyZaawansowanegoPolaWlasnego.Tekst => AdditionalFieldDataType.Text,
            TypSkalarnyZaawansowanegoPolaWlasnego.DlugiTekst => AdditionalFieldDataType.LongText,
            TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita => AdditionalFieldDataType.Integer,
            TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaRzeczywista => AdditionalFieldDataType.Decimal,
            TypSkalarnyZaawansowanegoPolaWlasnego.WartoscLogiczna => AdditionalFieldDataType.Boolean,
            TypSkalarnyZaawansowanegoPolaWlasnego.Data => AdditionalFieldDataType.Date,
            TypSkalarnyZaawansowanegoPolaWlasnego.Guid => AdditionalFieldDataType.Guid,
            _ => AdditionalFieldDataType.Unknown
        };

        private static AdditionalFieldDictionaryDto MapDictionary(ISlownikoweZrodloDanych dictionary) => dictionary.Rodzaj switch
        {
            RodzajSlownikowegoZrodlaDanych.SlownikWlasny => MapCustomDictionary(
                (ISlownikoweZrodloDanych<int, PozycjaSlownikaWlasnego>)dictionary),
            RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql => MapCustomSqlDictionary(dictionary),
            RodzajSlownikowegoZrodlaDanych.SlownikSystemowy => MapSystemDictionary(dictionary),
            _ => throw new NotSupportedException($"Unsupported dictionary kind: {dictionary.Rodzaj}.")
        };

        private static AdditionalFieldDictionaryDto MapCustomDictionary(
            ISlownikoweZrodloDanych<int, PozycjaSlownikaWlasnego> dictionary) => new(
                AdditionalFieldDictionaryKind.Custom,
                "int",
                null,
                dictionary.UtworzZapytanieFiltrowaneLinqTypowane()
                    .AsEnumerable()
                    .Select(item => new AdditionalFieldDictionaryOptionDto(
                        item.Id.ToString(CultureInfo.InvariantCulture),
                        item.Wartosc,
                        item.Aktywna))
                    .ToList());

        private static AdditionalFieldDictionaryDto MapCustomSqlDictionary(ISlownikoweZrodloDanych dictionary)
        {
            if (dictionary.TypKlucza == typeof(int))
            {
                var typedDictionary = (ISlownikoweZrodloDanych<int>)dictionary;
                return new AdditionalFieldDictionaryDto(
                    AdditionalFieldDictionaryKind.CustomSql,
                    "int",
                    null,
                    typedDictionary.UtworzZapytanieFiltrowaneLinq()
                        .AsEnumerable()
                        .Select(item => new AdditionalFieldDictionaryOptionDto(
                            item.Klucz.ToString(CultureInfo.InvariantCulture),
                            item.Wartosc,
                            null))
                        .ToList());
            }

            if (dictionary.TypKlucza == typeof(Guid))
            {
                var typedDictionary = (ISlownikoweZrodloDanych<Guid>)dictionary;
                return new AdditionalFieldDictionaryDto(
                    AdditionalFieldDictionaryKind.CustomSql,
                    "guid",
                    null,
                    typedDictionary.UtworzZapytanieFiltrowaneLinq()
                        .AsEnumerable()
                        .Select(item => new AdditionalFieldDictionaryOptionDto(
                            item.Klucz.ToString(),
                            item.Wartosc,
                            null))
                        .ToList());
            }

            return new AdditionalFieldDictionaryDto(
                AdditionalFieldDictionaryKind.CustomSql,
                dictionary.TypKlucza.FullName ?? dictionary.TypKlucza.Name,
                null,
                null);
        }

        private static AdditionalFieldDictionaryDto MapSystemDictionary(ISlownikoweZrodloDanych dictionary) => dictionary.Id switch
        {
            IdentyfikatorSlownikaSystemowego.Waluty => MapCurrencies(
                (ISlownikoweZrodloDanych<Guid, Waluta>)dictionary),
            IdentyfikatorSlownikaSystemowego.Magazyny => MapWarehouses(
                (ISlownikoweZrodloDanych<int, Magazyn>)dictionary),
            IdentyfikatorSlownikaSystemowego.RachunkiBankowe => MapBankAccounts(
                (ISlownikoweZrodloDanych<int, RachunekBankowy>)dictionary),
            _ => new AdditionalFieldDictionaryDto(
                AdditionalFieldDictionaryKind.System,
                dictionary.TypKlucza.FullName ?? dictionary.TypKlucza.Name,
                dictionary.Id.ToString(),
                null)
        };

        private static AdditionalFieldDictionaryDto MapCurrencies(
            ISlownikoweZrodloDanych<Guid, Waluta> dictionary) => new(
                AdditionalFieldDictionaryKind.System,
                "guid",
                "currencies",
                dictionary.UtworzZapytanieFiltrowaneLinqTypowane()
                    .AsEnumerable()
                    .Select(item => new AdditionalFieldDictionaryOptionDto(
                        item.Id.ToString(),
                        $"{item.Symbol} | {item.Nazwa}",
                        null))
                    .ToList());

        private static AdditionalFieldDictionaryDto MapWarehouses(
            ISlownikoweZrodloDanych<int, Magazyn> dictionary) => new(
                AdditionalFieldDictionaryKind.System,
                "int",
                "warehouses",
                dictionary.UtworzZapytanieFiltrowaneLinqTypowane()
                    .AsEnumerable()
                    .Select(item => new AdditionalFieldDictionaryOptionDto(
                        item.Id.ToString(CultureInfo.InvariantCulture),
                        $"{item.Symbol} | {item.Nazwa}",
                        null))
                    .ToList());

        private static AdditionalFieldDictionaryDto MapBankAccounts(
            ISlownikoweZrodloDanych<int, RachunekBankowy> dictionary) => new(
                AdditionalFieldDictionaryKind.System,
                "int",
                "bank-accounts",
                dictionary.UtworzZapytanieFiltrowaneLinqTypowane()
                    .AsEnumerable()
                    .Select(item => new AdditionalFieldDictionaryOptionDto(
                        item.Id.ToString(CultureInfo.InvariantCulture),
                        $"{item.Nazwa} | {item.Numer}",
                        null))
                    .ToList());
    }
}
