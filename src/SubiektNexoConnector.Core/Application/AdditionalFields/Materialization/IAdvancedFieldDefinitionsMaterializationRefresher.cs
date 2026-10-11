using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Materialization;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;

public interface IAdvancedFieldDefinitionsMaterializationRefresher
{
    Task<AdvancedFieldDefinitionsDto> GetCurrentAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default);
    Task<MaterializedResult<AdvancedFieldDefinitionsDto>> GetCurrentWithMetadataAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default);
    Task<AdvancedFieldDefinitionsMaterializationResult> RefreshIfDueAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default);
    Task<AdvancedFieldDefinitionsMaterializationResult> RefreshAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default);
}
