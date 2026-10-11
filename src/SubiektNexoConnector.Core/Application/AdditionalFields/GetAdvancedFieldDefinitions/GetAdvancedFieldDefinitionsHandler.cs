using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.GetAdvancedFieldDefinitions;

public sealed class GetAdvancedFieldDefinitionsHandler
{
    private readonly IAdvancedFieldDefinitionsMaterializationRefresher _materialization;

    public GetAdvancedFieldDefinitionsHandler(IAdvancedFieldDefinitionsMaterializationRefresher materialization)
    {
        _materialization = materialization;
    }

    public Task<MaterializedResult<AdvancedFieldDefinitionsDto>> HandleAsync(GetAdvancedFieldDefinitionsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _materialization.GetCurrentWithMetadataAsync(query.Target, cancellationToken);
    }
}
