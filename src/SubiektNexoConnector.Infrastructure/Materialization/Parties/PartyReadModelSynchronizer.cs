using Microsoft.Extensions.Logging;
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Parties;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Infrastructure.Materialization.Parties;

internal sealed class PartyReadModelSynchronizer : IPartyReadModelSynchronizer
{
    private readonly IAdvancedFieldDefinitionsReadModelReader _fieldDefinitionsReader;
    private readonly IPartyReadModelWriter _writer;
    private readonly ILogger<PartyReadModelSynchronizer> _logger;

    public PartyReadModelSynchronizer(IAdvancedFieldDefinitionsReadModelReader fieldDefinitionsReader, IPartyReadModelWriter writer, ILogger<PartyReadModelSynchronizer> logger)
    {
        _fieldDefinitionsReader = fieldDefinitionsReader;
        _writer = writer;
        _logger = logger;
    }

    public async Task<bool> TryUpsertAsync(PartyDetailsDto party, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(party);

        try
        {
            var definitions = party.AdvancedFields.Count == 0
                ? new Dictionary<string, AdvancedFieldDefinitionDto>(StringComparer.Ordinal)
                : ToDefinitionDictionary(await _fieldDefinitionsReader.GetAsync(AdditionalFieldTarget.Party, cancellationToken));

            await _writer.UpsertAsync(party, definitions, DateTime.UtcNow, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Party {PartySignature} was saved in Nexo, but its SQLite materialization could not be updated.", party.Signature);
            return false;
        }
    }

    private static IReadOnlyDictionary<string, AdvancedFieldDefinitionDto> ToDefinitionDictionary(AdvancedFieldDefinitionsDto definitions)
    {
        var definitionsWithoutGroup = definitions.Fields;
        var definitionsFromGroups = definitions.Groups.SelectMany(group => group.Fields);
        var allDefinitions = definitionsWithoutGroup.Concat(definitionsFromGroups);

        return allDefinitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
    }
}
