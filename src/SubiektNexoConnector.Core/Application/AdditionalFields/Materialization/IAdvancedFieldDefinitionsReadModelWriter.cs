
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.Materialization;

public interface IAdvancedFieldDefinitionsReadModelWriter
{
    Task ReplaceAsync(AdvancedFieldDefinitionsDto definitions, CancellationToken cancellationToken = default);
}
