using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Infrastructure.Materialization.Persistence;

namespace SubiektNexoConnector.Infrastructure.Materialization.AdditionalFields;

internal sealed class AdvancedFieldDefinitionsReadModelWriter : IAdvancedFieldDefinitionsReadModelWriter
{
    private readonly ReadModelDbContext _dbContext;

    public AdvancedFieldDefinitionsReadModelWriter(ReadModelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ReplaceAsync(AdvancedFieldDefinitionsDto definitions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        cancellationToken.ThrowIfCancellationRequested();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingDefinitions = await _dbContext.AdvancedFieldDefinitions
                .Where(definition => definition.Target == definitions.Target)
                .ToListAsync(cancellationToken);
            var existingGroups = await _dbContext.AdvancedFieldGroups
                .Where(group => group.Target == definitions.Target)
                .ToListAsync(cancellationToken);

            _dbContext.AdvancedFieldDefinitions.RemoveRange(existingDefinitions);
            _dbContext.AdvancedFieldGroups.RemoveRange(existingGroups);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var newGroups = new List<AdvancedFieldGroupEntity>(definitions.Groups.Count);
            var newDefinitions = definitions.Fields
                .Select((definition, position) => MapDefinition(
                    definition,
                    definitions.Target,
                    position,
                    group: null))
                .ToList();

            foreach (var group in definitions.Groups)
            {
                var groupEntity = new AdvancedFieldGroupEntity
                {
                    Target = definitions.Target,
                    Name = group.Name,
                    Position = group.Position
                };

                newGroups.Add(groupEntity);
                newDefinitions.AddRange(group.Fields.Select((definition, position) => MapDefinition(
                    definition,
                    definitions.Target,
                    position,
                    groupEntity)));
            }

            _dbContext.AdvancedFieldGroups.AddRange(newGroups);
            _dbContext.AdvancedFieldDefinitions.AddRange(newDefinitions);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _dbContext.ChangeTracker.Clear();
        }
    }

    private static AdvancedFieldDefinitionEntity MapDefinition(AdvancedFieldDefinitionDto definition, AdditionalFieldTarget target, int position, AdvancedFieldGroupEntity? group)
    {
        var entity = new AdvancedFieldDefinitionEntity
        {
            Target = target,
            FieldId = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            DataType = definition.DataType,
            Required = definition.Required,
            Visible = definition.Visible,
            Editable = definition.Editable,
            Cloneable = definition.Cloneable,
            Precision = definition.Precision,
            MinVisibleLines = definition.MinVisibleLines,
            MaxVisibleLines = definition.MaxVisibleLines,
            DefaultValueJson = definition.DefaultValue is null
                ? null
                : JsonSerializer.Serialize(definition.DefaultValue),
            Position = position,
            DictionaryKind = definition.Dictionary?.Kind,
            DictionaryKeyType = definition.Dictionary?.KeyType,
            SystemDictionary = definition.Dictionary?.SystemDictionary,
            DictionaryOptionsAvailable = definition.Dictionary?.Options is not null,
            Group = group
        };

        if (definition.Dictionary?.Options is not null)
        {
            entity.DictionaryOptions = definition.Dictionary.Options
                .Select((option, optionPosition) => new AdvancedFieldDictionaryOptionEntity
                {
                    Key = option.Key,
                    Label = option.Label,
                    IsActive = option.IsActive,
                    Position = optionPosition
                })
                .ToList();
        }

        return entity;
    }
}
