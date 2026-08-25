using System.Globalization;
using System.Text.Json;
using InsERT.Moria.Narzedzia.PolaWlasne2;
using InsERT.Moria.PolaWlasne2;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Infrastructure.Nexo.Common;

internal static class NexoAdditionalFieldValueMapper
{
    public static void ApplyBasic(
        IProstePolaWlasne definitions,
        IPolaWlasneStdAccessor values,
        Type targetType,
        IReadOnlyCollection<AdditionalFieldValueDto> fields)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(fields);

        var definitionsById = definitions
            .PobierzProstePolaWlasne(targetType)
            .ToDictionary(field => field.Id, StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (!definitionsById.ContainsKey(field.Id))
                throw new InvalidOperationException($"Unknown basic field id: {field.Id}.");

            values.UstawWartoscPoId(field.Id, GetTextValue(field));
        }
    }

    public static void ApplyAdvanced(
        IEnumerable<IZaawansowanePoleWlasne> definitions,
        IPolaWlasneAdv2Accessor values,
        IReadOnlyCollection<AdditionalFieldValueDto> fields)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(fields);

        var definitionsById = definitions.ToDictionary(field => field.Id, StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (!definitionsById.TryGetValue(field.Id, out var definition))
                throw new InvalidOperationException($"Unknown advanced field id: {field.Id}.");

            SetAdvancedValue(definition, values, field);
        }
    }

    public static IReadOnlyCollection<AdditionalFieldValueDto> MapBasic(
        IProstePolaWlasne definitions,
        IPolaWlasneStdAccessor values,
        Type targetType)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(targetType);

        if (!definitions.MaProstePolaWlasne(targetType))
            return [];

        return definitions
            .PobierzProstePolaWlasne(targetType)
            .Where(field => field.Widoczne)
            .Select(field => new AdditionalFieldValueDto(
                field.Id,
                values.PobierzWartoscPoId(field.Id)))
            .ToList();
    }

    public static IReadOnlyCollection<AdditionalFieldValueDto> MapAdvanced(
        IEnumerable<IZaawansowanePoleWlasne> definitions,
        IPolaWlasneAdv2Accessor values)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(values);

        return definitions
            .Where(field => field.Widoczne)
            .Select(field => new AdditionalFieldValueDto(
                field.Id,
                GetAdvancedValue(field, values)))
            .ToList();
    }

    #region Advanced field write mapping

    private static void SetAdvancedValue(
        IZaawansowanePoleWlasne definition,
        IPolaWlasneAdv2Accessor values,
        AdditionalFieldValueDto field)
    {
        if (definition.JestReferencjaDoSlownika)
        {
            SetDictionaryValue(definition, values, field);
            return;
        }

        switch (definition.Typ)
        {
            case TypSkalarnyZaawansowanegoPolaWlasnego.Tekst:
            case TypSkalarnyZaawansowanegoPolaWlasnego.DlugiTekst:
                values.UstawWartoscTypuTekst(definition.Nazwa, GetTextValue(field));
                return;
            case TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita:
                values.UstawWartoscTypuLiczbaCalkowita(definition.Nazwa, GetIntegerValue(field));
                return;
            case TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaRzeczywista:
                values.UstawWartoscTypuLiczbaRzeczywista(definition.Nazwa, GetDecimalValue(field));
                return;
            case TypSkalarnyZaawansowanegoPolaWlasnego.WartoscLogiczna:
                values.UstawWartoscTypuLogicznego(definition.Nazwa, GetBooleanValue(field));
                return;
            case TypSkalarnyZaawansowanegoPolaWlasnego.Data:
                values.UstawWartoscTypuData(definition.Nazwa, GetDateValue(field));
                return;
            default:
                throw new NotSupportedException(
                    $"Unsupported advanced field type: {definition.Typ}.");
        }
    }

    private static void SetDictionaryValue(
        IZaawansowanePoleWlasne definition,
        IPolaWlasneAdv2Accessor values,
        AdditionalFieldValueDto field)
    {
        switch (definition.RodzajSlownika)
        {
            case RodzajSlownikowegoZrodlaDanych.SlownikWlasny:
                values.UstawWartoscTypuSlownikWlasny(definition.Nazwa, GetIntegerValue(field));
                return;
            case RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql when
                definition.Typ == TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita:
                values.UstawWartoscTypuSlownikWlasnySqlByInt(definition.Nazwa, GetIntegerValue(field));
                return;
            case RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql when
                definition.Typ == TypSkalarnyZaawansowanegoPolaWlasnego.Guid:
                values.UstawWartoscTypuSlownikWlasnySqlByGuid(definition.Nazwa, GetGuidValue(field));
                return;
            case RodzajSlownikowegoZrodlaDanych.SlownikSystemowy when
                definition.PobierzDefinicjeSlownika().Id == IdentyfikatorSlownikaSystemowego.Waluty:
                values.UstawWartoscTypuSlownikSystemowyWalut(definition.Nazwa, GetGuidValue(field));
                return;
            case RodzajSlownikowegoZrodlaDanych.SlownikSystemowy when
                definition.PobierzDefinicjeSlownika().Id == IdentyfikatorSlownikaSystemowego.Magazyny:
                values.UstawWartoscTypuSlownikSystemowyMagazynow(definition.Nazwa, GetIntegerValue(field));
                return;
            case RodzajSlownikowegoZrodlaDanych.SlownikSystemowy when
                definition.PobierzDefinicjeSlownika().Id == IdentyfikatorSlownikaSystemowego.RachunkiBankowe:
                values.UstawWartoscTypuSlownikSystemowyRachunkowBankowych(definition.Nazwa, GetIntegerValue(field));
                return;
            default:
                throw new NotSupportedException(
                    $"Unsupported advanced field dictionary: {definition.RodzajSlownika}.");
        }
    }

    private static string? GetTextValue(AdditionalFieldValueDto field)
    {
        if (field.Value is null)
            return null;

        if (field.Value is string value)
            return value;

        if (field.Value is JsonElement { ValueKind: JsonValueKind.Null })
            return null;

        if (field.Value is JsonElement { ValueKind: JsonValueKind.String } element)
            return element.GetString();

        throw InvalidFieldValue(field, "a string");
    }

    private static int? GetIntegerValue(AdditionalFieldValueDto field)
    {
        if (field.Value is null || field.Value is JsonElement { ValueKind: JsonValueKind.Null })
            return null;

        if (field.Value is int value)
            return value;

        if (field.Value is long longValue && longValue is >= int.MinValue and <= int.MaxValue)
            return (int)longValue;

        if (field.Value is string text
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stringValue))
        {
            return stringValue;
        }

        if (field.Value is JsonElement { ValueKind: JsonValueKind.Number } element
            && element.TryGetInt32(out var jsonValue))
        {
            return jsonValue;
        }

        throw InvalidFieldValue(field, "an integer");
    }

    private static decimal? GetDecimalValue(AdditionalFieldValueDto field)
    {
        if (field.Value is null || field.Value is JsonElement { ValueKind: JsonValueKind.Null })
            return null;

        if (field.Value is decimal value)
            return value;

        if (field.Value is byte or short or int or long or float or double)
            return Convert.ToDecimal(field.Value, CultureInfo.InvariantCulture);

        if (field.Value is string text && TryParseDecimal(text, out var stringValue))
        {
            return stringValue;
        }

        if (field.Value is JsonElement { ValueKind: JsonValueKind.String } stringElement
            && TryParseDecimal(stringElement.GetString(), out var jsonStringValue))
        {
            return jsonStringValue;
        }

        if (field.Value is JsonElement { ValueKind: JsonValueKind.Number } element
            && element.TryGetDecimal(out var jsonValue))
        {
            return jsonValue;
        }

        throw InvalidFieldValue(field, "a decimal number");
    }

    private static bool? GetBooleanValue(AdditionalFieldValueDto field)
    {
        if (field.Value is null || field.Value is JsonElement { ValueKind: JsonValueKind.Null })
            return null;

        if (field.Value is bool value)
            return value;

        if (field.Value is JsonElement { ValueKind: JsonValueKind.True })
            return true;

        if (field.Value is JsonElement { ValueKind: JsonValueKind.False })
            return false;

        throw InvalidFieldValue(field, "a boolean");
    }

    private static DateTime? GetDateValue(AdditionalFieldValueDto field)
    {
        if (field.Value is null || field.Value is JsonElement { ValueKind: JsonValueKind.Null })
            return null;

        if (field.Value is DateTime value)
            return value;

        if (field.Value is JsonElement { ValueKind: JsonValueKind.String } element
            && element.TryGetDateTime(out var jsonValue))
        {
            return jsonValue;
        }

        throw InvalidFieldValue(field, "an ISO 8601 date");
    }

    private static Guid? GetGuidValue(AdditionalFieldValueDto field)
    {
        if (field.Value is null || field.Value is JsonElement { ValueKind: JsonValueKind.Null })
            return null;

        if (field.Value is Guid value)
            return value;

        if (field.Value is JsonElement { ValueKind: JsonValueKind.String } element
            && element.TryGetGuid(out var jsonValue))
        {
            return jsonValue;
        }

        throw InvalidFieldValue(field, "a GUID");
    }

    private static bool TryParseDecimal(string? value, out decimal parsedValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsedValue = default;
            return false;
        }

        var culture = value.Contains(",", StringComparison.Ordinal)
            ? CultureInfo.GetCultureInfo("pl-PL")
            : CultureInfo.InvariantCulture;

        return decimal.TryParse(value, NumberStyles.Number, culture, out parsedValue);
    }

    private static InvalidOperationException InvalidFieldValue(
        AdditionalFieldValueDto field,
        string expectedType) => new(
        $"Additional field '{field.Id}' must contain {expectedType}.");

    #endregion

    private static object? GetAdvancedValue(
        IZaawansowanePoleWlasne field,
        IPolaWlasneAdv2Accessor values)
    {
        if (field.JestReferencjaDoSlownika)
            return GetDictionaryKey(field, values);

        return field.Typ switch
        {
            TypSkalarnyZaawansowanegoPolaWlasnego.Tekst or
            TypSkalarnyZaawansowanegoPolaWlasnego.DlugiTekst =>
                values.PobierzWartoscTypuTekst(field.Nazwa),
            TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita =>
                values.PobierzWartoscTypuLiczbaCalkowita(field.Nazwa),
            TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaRzeczywista =>
                values.PobierzWartoscTypuLiczbaRzeczywista(field.Nazwa),
            TypSkalarnyZaawansowanegoPolaWlasnego.WartoscLogiczna =>
                values.PobierzWartoscTypuLogicznego(field.Nazwa),
            TypSkalarnyZaawansowanegoPolaWlasnego.Data =>
                values.PobierzWartoscTypuData(field.Nazwa),
            _ => throw new NotSupportedException(
                $"Unsupported advanced field type: {field.Typ}.")
        };
    }

    private static object? GetDictionaryKey(
        IZaawansowanePoleWlasne field,
        IPolaWlasneAdv2Accessor values) => field.RodzajSlownika switch
        {
            RodzajSlownikowegoZrodlaDanych.SlownikWlasny =>
                values.PobierzWartoscTypuSlownikWlasny2(field.Nazwa),
            RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql when
                field.Typ == TypSkalarnyZaawansowanegoPolaWlasnego.LiczbaCalkowita =>
                values.PobierzWartoscTypuSlownikWlasnySqlByInt2(field.Nazwa),
            RodzajSlownikowegoZrodlaDanych.SlownikWlasnySql when
                field.Typ == TypSkalarnyZaawansowanegoPolaWlasnego.Guid =>
                values.PobierzWartoscTypuSlownikWlasnySqlByGuid2(field.Nazwa),
            RodzajSlownikowegoZrodlaDanych.SlownikSystemowy when
                field.PobierzDefinicjeSlownika().Id == IdentyfikatorSlownikaSystemowego.Waluty =>
                values.PobierzWartoscTypuSlownikSystemowyWalut2(field.Nazwa),
            RodzajSlownikowegoZrodlaDanych.SlownikSystemowy when
                field.PobierzDefinicjeSlownika().Id == IdentyfikatorSlownikaSystemowego.Magazyny =>
                values.PobierzWartoscTypuSlownikSystemowyMagazynow2(field.Nazwa),
            RodzajSlownikowegoZrodlaDanych.SlownikSystemowy when
                field.PobierzDefinicjeSlownika().Id == IdentyfikatorSlownikaSystemowego.RachunkiBankowe =>
                values.PobierzWartoscTypuSlownikSystemowyRachunkowBankowych2(field.Nazwa),
            _ => throw new NotSupportedException(
                $"Unsupported advanced field dictionary: {field.RodzajSlownika}.")
        };
}
