using System.ComponentModel.DataAnnotations;

namespace SubiektNexoConnector.Core.Application.Parties.Contacts.CreateContact;

public sealed record CreatePartyContactRequestDto(
    [Range(1, int.MaxValue)] int ContactTypeId,
    string? Value,
    bool IsPrimary,
    string? Comment);
