namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;

internal sealed class PartyContactEntity
{
    public int PartyId { get; set; }
    public int ContactId { get; set; }
    public bool IsPrimary { get; set; }
    public int ContactTypeId { get; set; }
    public string? ContactType { get; set; }
    public string? ContactValue { get; set; }
    public string? ContactDescription { get; set; }
    public int Position { get; set; }
    public DateTime RefreshedAtUtc { get; set; }
}
