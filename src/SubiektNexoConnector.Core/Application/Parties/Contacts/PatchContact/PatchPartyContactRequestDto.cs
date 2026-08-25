using System.Text.Json.Serialization;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.PatchContact
{
    public sealed record PatchPartyContactRequestDto(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<bool> IsPrimary,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> ContactValue,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Optional<string?> ContactDescription
    );
}
