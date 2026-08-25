using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact
{
    public sealed record PatchPartyContactCommand
    (
        string PartySignature,
        int ContactId,
        Optional<bool> IsPrimary,
        Optional<string?> ContactValue,
        Optional<string?> ContactDescription
    );
}
