using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;

public interface IAdvancedFieldDefinitionsReadModelReader
{
    Task<AdvancedFieldDefinitionsDto> GetAsync(AdditionalFieldTarget target, CancellationToken cancellationToken = default);
}
