using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using SubiektNexoConnector.Core.Application.AdditionalFields.Shared;
using SubiektNexoConnector.Core.Application.Common;
using SubiektNexoConnector.Core.Application.Parties.Addresses.Shared;
using SubiektNexoConnector.Core.Application.Parties.Contacts.Shared;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Infrastructure.Nexo.Common
{
    internal static class NexoPartyMapper
    {
        public static PartyBasicDto MapBasic(Podmiot party) => new(
            party.Id,
            party.Sygnatura.PelnaSygnatura,
            party.NazwaSkrocona,
            MapClassification(party),
            party.NIP,
            party.Aktywny,
            party.Osoba?.Imie,
            party.Osoba?.Nazwisko,
            party.Firma?.Nazwa);

        public static PartyDetailsDto MapDetails(Podmiot party, IReadOnlyCollection<AdditionalFieldValueDto> basicFields, IReadOnlyCollection<AdditionalFieldValueDto> advancedFields) => new(
            party.Id,
            party.Sygnatura.PelnaSygnatura,
            party.NazwaSkrocona,
            party.Aktywny,
            MapClassification(party),
            party.Osoba?.Imie,
            party.Osoba?.Nazwisko,
            party.Firma?.Nazwa,
            party.NIP,
            party.NIPUE,
            party.Firma?.REGON,
            party.Firma?.KRS,
            party.Grupy.FirstOrDefault()?.Nazwa,
            party.Branze.Select(industry => industry.Nazwa).ToList(),
            party.Cechy.Select(feature => feature.Nazwa).ToList(),
            party.Uwagi,
            party.FlagaWlasna is null ? null : new FlagAssignmentDto(party.FlagaWlasna.Id, party.FlagHeader?.Description),
            basicFields,
            advancedFields,
            party.Adresy.Select(MapAddress).ToList(),
            party.Kontakty.Select(MapContact).ToList(),
            MapTradeCreditLimit(party));

        public static PartyClassificationDto MapClassification(Podmiot party)
        {
            var type = (TypObiektu)party.Typ;
            var subtype = (PodtypPodmiotu)party.Podtyp;
            var customerStatus = (StatusKlienta)party.StatusKlienta;

            return new PartyClassificationDto(
                party.Typ,
                NexoEnumDisplayName.Get(type),
                party.Podtyp,
                NexoEnumDisplayName.Get(subtype),
                party.StatusKlienta,
                NexoEnumDisplayName.Get(customerStatus));
        }

        public static PartyAddressDto MapAddress(Adres address) => new(
            address.Id,
            address.Szczegoly.Ulica,
            address.Szczegoly.NrDomu,
            address.Szczegoly.NrLokalu,
            address.Szczegoly.KodPocztowy,
            address.Szczegoly.Miejscowosc,
            address.Szczegoly.Gmina?.Nazwa,
            address.Szczegoly.Wojewodztwo?.Nazwa,
            address.Panstwo?.Id,
            address.TypAdresu.Nazwa,
            address.TypAdresu.Id);

        public static PartyContactDto MapContact(Kontakt contact) => new(
            contact.Id,
            contact.Podstawowy,
            contact.Rodzaj.Id,
            contact.Rodzaj.Nazwa,
            contact.Wartosc,
            contact.Komentarz);

        public static TradeCreditLimitDto MapTradeCreditLimit(Podmiot party) => new(
            party.ZezwalajNaKredytKupiecki,
            party.MaksymalnyTerminPlatnosciKredytu,
            party.MaksymalnyLiczbaDniSpoznien,
            party.MaksymalnaLiczbaNiesplaconychDok,
            party.LimitKredytuKupieckiego,
            MapDocumentTradeCreditLimit(party.LimitKredytuNaSprzedazyAktywny, party.LimitKredytuNaSprzedazy),
            MapDocumentTradeCreditLimit(party.LimitKredytuNaWydaniuAktywny, party.LimitKredytuNaWydaniu),
            MapDocumentTradeCreditLimit(party.LimitKredytuNaZamowieniuAktywny, party.LimitKredytuNaZamowieniu));

        private static DocumentTradeCreditLimitDto MapDocumentTradeCreditLimit(
            bool active,
            LimitKredytuKupieckiego? limit)
        {
            if (!active || limit is null)
                return new DocumentTradeCreditLimitDto(false, null, null, null);

            return new DocumentTradeCreditLimitDto(
                true,
                limit.Wartosc,
                limit.LimitPonizejWartosci,
                limit.LimitPowyzejWartosci);
        }
    }
}
