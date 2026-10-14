namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyIndustryEntity
{
    public int PartyId { get; set; }
    public required string Name { get; set; }
    public int Position { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
