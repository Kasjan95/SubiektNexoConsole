using SubiektNexoConnector.Core.Application.Parties.Addresses.CreateAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.DeleteAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.PatchAddress;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.DeleteContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;

namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public interface IPartyRepository
    {
        PartyCreateOptionsDto GetCreateOptions();

        PartyDetailsDto CreateParty(CreatePartyCommand command);
        IReadOnlyCollection<PartyBasicDto> GetAllParties(GetPartiesQuery query);
        PartyDetailsDto? GetDetailsParty(GetPartyDetailsQuery query);
        string? PatchParty(PatchPartyCommand command);

        CreatePartyAddressResult? CreatePartyAddress(CreatePartyAddressCommand command);
        PartyAddressDto? PatchPartyAddress(PatchPartyAddressCommand command);
        DeletePartyResourceResult DeletePartyAddress(DeletePartyAddressCommand command);

        PartyContactDto? CreatePartyContact(CreatePartyContactCommand command);
        DeletePartyResourceResult DeletePartyContact(DeletePartyContactCommand command);
        PartyContactDto? PatchPartyContact(PatchPartyContactCommand command);
    }
}
