using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization.AdditionalFields;

internal sealed class AdvancedFieldDefinitionsReadModelReader : IAdvancedFieldDefinitionsReadModelReader
{
    private readonly ReadModelDbContext _dbContext;

    public AdvancedFieldDefinitionsReadModelReader(ReadModelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdvancedFieldDefinitionsDto> GetAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default)
    {
        var groups = await _dbContext.AdvancedFieldGroups
            .AsNoTracking()
            .Where(group => group.Target == target)
            .OrderBy(group => group.Position)
            .ThenBy(group => group.Name)
            .ToListAsync(cancellationToken);

        var definitions = await _dbContext.AdvancedFieldDefinitions
            .AsNoTracking()
            .Include(definition => definition.DictionaryOptions)
            .Where(definition => definition.Target == target)
            .ToListAsync(cancellationToken);

        var definitionsByGroupId = definitions
            .Where(definition => definition.GroupId.HasValue)
            .GroupBy(definition => definition.GroupId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(definition => definition.Position).ThenBy(definition => definition.FieldId).ToArray());

        var mappedGroups = groups
            .Select(group => new AdvancedFieldGroupDto(
                group.Name,
                group.Position,
                definitionsByGroupId.TryGetValue(group.Id, out var groupDefinitions)
                    ? groupDefinitions.Select(MapDefinition).ToArray()
                    : []))
            .ToArray();

        var ungroupedDefinitions = definitions
            .Where(definition => definition.GroupId is null)
            .OrderBy(definition => definition.Position)
            .ThenBy(definition => definition.FieldId)
            .Select(MapDefinition)
            .ToArray();

        return new AdvancedFieldDefinitionsDto(target, mappedGroups, ungroupedDefinitions);
    }

    private static AdvancedFieldDefinitionDto MapDefinition(AdvancedFieldDefinitionEntity definition) => new(
        definition.FieldId,
        definition.Name,
        definition.Description,
        definition.DataType,
        definition.Required,
        definition.Visible,
        definition.Editable,
        definition.Cloneable,
        definition.Precision,
        definition.MinVisibleLines,
        definition.MaxVisibleLines,
        DeserializeDefaultValue(definition),
        MapDictionary(definition));

    private static AdvancedFieldDictionaryDto? MapDictionary(AdvancedFieldDefinitionEntity definition)
    {
        if (!definition.DictionaryKind.HasValue)
            return null;

        var keyType = definition.DictionaryKeyType
            ?? throw new InvalidOperationException($"Advanced field '{definition.FieldId}' has no dictionary key type.");
        IReadOnlyCollection<AdvancedFieldDictionaryOptionDto>? options = definition.DictionaryOptionsAvailable
            ? definition.DictionaryOptions
                .OrderBy(option => option.Position)
                .ThenBy(option => option.Key)
                .Select(option => new AdvancedFieldDictionaryOptionDto(option.Key, option.Label, option.IsActive))
                .ToArray()
            : null;

        return new AdvancedFieldDictionaryDto(definition.DictionaryKind.Value, keyType, definition.SystemDictionary, options);
    }

    private static object? DeserializeDefaultValue(AdvancedFieldDefinitionEntity definition)
    {
        if (definition.DefaultValueJson is null)
            return null;

        try
        {
            return definition.DataType switch
            {
                AdvancedFieldDataType.Text => JsonSerializer.Deserialize<string>(definition.DefaultValueJson),
                AdvancedFieldDataType.LongText => JsonSerializer.Deserialize<string>(definition.DefaultValueJson),
                AdvancedFieldDataType.Integer => JsonSerializer.Deserialize<int>(definition.DefaultValueJson),
                AdvancedFieldDataType.Decimal => JsonSerializer.Deserialize<decimal>(definition.DefaultValueJson),
                AdvancedFieldDataType.Boolean => JsonSerializer.Deserialize<bool>(definition.DefaultValueJson),
                AdvancedFieldDataType.Date => JsonSerializer.Deserialize<DateTime>(definition.DefaultValueJson),
                AdvancedFieldDataType.Guid => JsonSerializer.Deserialize<Guid>(definition.DefaultValueJson),
                AdvancedFieldDataType.Dictionary => DeserializeDictionaryDefaultValue(definition),
                _ => JsonSerializer.Deserialize<JsonElement>(definition.DefaultValueJson)
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Invalid materialized default value for advanced field '{definition.FieldId}'.", exception);
        }
    }

    private static object DeserializeDictionaryDefaultValue(AdvancedFieldDefinitionEntity definition)
    {
        if (definition.DictionaryKeyType?.Equals("int", StringComparison.OrdinalIgnoreCase) == true
            || definition.DictionaryKeyType?.Equals("System.Int32", StringComparison.Ordinal) == true)
        {
            return JsonSerializer.Deserialize<int>(definition.DefaultValueJson!);
        }

        if (definition.DictionaryKeyType?.Equals("guid", StringComparison.OrdinalIgnoreCase) == true
            || definition.DictionaryKeyType?.Equals("System.Guid", StringComparison.Ordinal) == true)
        {
            return JsonSerializer.Deserialize<Guid>(definition.DefaultValueJson!);
        }

        return JsonSerializer.Deserialize<JsonElement>(definition.DefaultValueJson!);
    }
}
