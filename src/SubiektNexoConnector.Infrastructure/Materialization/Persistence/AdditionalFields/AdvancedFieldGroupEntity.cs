using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;

namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;
internal sealed class AdvancedFieldGroupEntity
{
    public long Id { get; set; }
    public AdditionalFieldTarget Target { get; set; }
    public required string Name { get; set; }
    public int Position { get; set; }
}
