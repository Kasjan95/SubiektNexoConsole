using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.AdditionalFields.GetFlagDefinitions
{
    public sealed record GetFlagDefinitionQuery(
        Optional<int?> Domain
    );
}
