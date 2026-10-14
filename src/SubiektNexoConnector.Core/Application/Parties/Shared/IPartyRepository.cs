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
using SubiektNexoConnector.Core.Application.AdditionalFields.AdvancedFieldDefinitions.Shared;
using SubiektNexoConnector.Core.Application.Parties;

namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public interface IPartyRepository
    {
        PartyCreateOptionsDto GetCreateOptions();

        PartyDetailsDto CreateParty(CreatePartyCommand command);
        IReadOnlyCollection<PartyBasicDto> GetAllParties(GetPartiesQuery query);
        PartyDetailsDto? GetDetailsParty(GetPartyDetailsQuery query);
        PartyDetailsDto? GetDetailsForMaterialization(string signature, IReadOnlyCollection<AdvancedFieldDefinitionDto> advancedFieldDefinitions, CancellationToken cancellationToken = default);
        PartyMaterializationSourceBatch GetDetailsBatchAfterId(int? afterId, int batchSize, IReadOnlyCollection<AdvancedFieldDefinitionDto> advancedFieldDefinitions, CancellationToken cancellationToken = default);
        PartyDetailsDto? PatchParty(PatchPartyCommand command);

        CreatePartyAddressResult? CreatePartyAddress(CreatePartyAddressCommand command);
        PartyDetailsDto? PatchPartyAddress(PatchPartyAddressCommand command);
        PartyDetailsDto? DeletePartyAddress(DeletePartyAddressCommand command);

        PartyDetailsDto? CreatePartyContact(CreatePartyContactCommand command);
        PartyDetailsDto? DeletePartyContact(DeletePartyContactCommand command);
        PartyDetailsDto? PatchPartyContact(PatchPartyContactCommand command);
    }
}
