namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public interface IPartyRepository
    {
        IReadOnlyCollection<PartyBasicDto> GetAll();
    }
}
