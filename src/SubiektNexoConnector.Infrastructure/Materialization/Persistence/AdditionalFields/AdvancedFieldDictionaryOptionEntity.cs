namespace SubiektNexoConnector.Infrastructure.Materialization.Persistence;
internal sealed class AdvancedFieldDictionaryOptionEntity
{
    public long DefinitionId { get; set; }
    public required string Key { get; set; }
    public required string Label { get; set; }
    public bool? IsActive { get; set; }
    public int Position { get; set; }
}
