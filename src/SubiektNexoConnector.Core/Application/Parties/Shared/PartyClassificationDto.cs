namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public sealed record PartyClassificationDto(
        short Type, 
        string TypeName, 
        byte? Subtype, 
        string SubtypeName, 
        int CustomerStatus, 
        string CustomerStatusName);
}
