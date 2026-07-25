using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.GetPartyDetails;
using SubiektNexoConnector.Core.Application.Parties.PatchParty;
using SubiektNexoConnector.Core.Application.Parties.Shared;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Nexo.Common;
using System.Text;

namespace SubiektNexoConnector.Infrastructure.Nexo
{
    public class NexoPartyRepository : IPartyRepository
    {
        private const int MaxPageSize = 1000;
        private readonly ISessionFactory _sessionFactory;

        public NexoPartyRepository(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }
        #region GET
        public IReadOnlyCollection<PartyBasicDto> GetAll(GetPartiesQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            using Uchwyt sfera = _sessionFactory.Create();

            var parties = query.CustomerStatus switch
            {
                PartyCustomerStatusFilter.Standard => sfera.Podmioty().Dane.Wszystkie(a => (int)a.StatusKlienta == 0),
                PartyCustomerStatusFilter.Potential => sfera.Podmioty().Dane.Wszystkie(a => (int)a.StatusKlienta == 2),
                PartyCustomerStatusFilter.All => sfera.Podmioty().Dane.Wszystkie(),
                _ => sfera.Podmioty().Dane.Wszystkie(a => (int)a.StatusKlienta == 0)
            };

            if (query.Type.HasValue)
            {
                parties = parties.Where(a => a.Typ == query.Type.Value);
            }

            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            var mappedParties = parties
                .OrderBy(a => a.Id)
                .ToList()
                .Select(a => new PartyBasicDto(
                    a.Sygnatura.PelnaSygnatura,
                    a.NazwaSkrocona,
                    a.Typ,
                    a.Podtyp,
                    PartyCodeDictionary.GetTypeName(a.Typ),
                    a.PobierzNazweGrupyTypu(),
                    (int)a.StatusKlienta,
                    PartyCodeDictionary.GetCustomerStatusName((int)a.StatusKlienta),
                    a.NIP,
                    a.Aktywny,
                    a.Osoba?.Imie,
                    a.Osoba?.Nazwisko,
                    a.Firma?.Nazwa));

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchTerm = query.Search.Trim();
                mappedParties = mappedParties.Where(a =>
                    SearchTextMatcher.MatchesSearch(a, searchTerm));
            }

            return mappedParties
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        public PartyDetailsDto? GetDetails(GetPartyDetailsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            using Uchwyt sfera = _sessionFactory.Create();
            Podmiot? party = sfera.Podmioty().Dane.Wszystkie(a => a.Sygnatura.PelnaSygnatura == query.PartySignature).FirstOrDefault();
            if (party == null)
                return null;

            List<PartyAddressDto> addresses = party.Adresy.Select(a => new PartyAddressDto(
                a.Id,
                a.Szczegoly.Ulica,
                a.Szczegoly.NrDomu,
                a.Szczegoly.NrLokalu,
                a.Szczegoly.Miejscowosc,
                a.Szczegoly.Gmina?.Nazwa,
                a.Szczegoly.Wojewodztwo?.Nazwa,
                a.Panstwo?.Nazwa,
                a.TypAdresu.Nazwa,
                a.TypAdresu.Id)).ToList();

            TradeCreditLimitDto tradeCreditLimit = new TradeCreditLimitDto(
                party.ZezwalajNaKredytKupiecki,
                party.MaksymalnyTerminPlatnosciKredytu,
                party.MaksymalnyLiczbaDniSpoznien,
                party.MaksymalnaLiczbaNiesplaconychDok,
                party.LimitKredytuKupieckiego,
                MapDocumentTradeCreditLimit(party.LimitKredytuNaSprzedazyAktywny, party.LimitKredytuNaSprzedazy),
                MapDocumentTradeCreditLimit(party.LimitKredytuNaWydaniuAktywny, party.LimitKredytuNaWydaniu),
                MapDocumentTradeCreditLimit(party.LimitKredytuNaZamowieniuAktywny, party.LimitKredytuNaZamowieniu));

            List<PartyContactDto> contacts = party.Kontakty.Select(c => new PartyContactDto(
                c.Id,
                c.Podstawowy,
                c.Rodzaj.Nazwa,
                c.Wartosc,
                c.Komentarz)).ToList();

            return new PartyDetailsDto(
                party.Sygnatura.PelnaSygnatura,
                party.NazwaSkrocona,
                party.Aktywny,
                PartyCodeDictionary.GetTypeName(party.Typ),
                party.PobierzNazweGrupyTypu(),
                party.Osoba?.Imie,
                party.Osoba?.Nazwisko,
                party.Firma?.Nazwa,
                party.NIP,
                party.NIPUE,
                party.Firma?.REGON,
                party.Firma?.KRS,
                party.Grupy.FirstOrDefault()?.Nazwa,
                party.Branze.Select(industry => industry.Nazwa).ToList(),
                party.Cechy.Select(c => c.Nazwa).ToList(),
                party.Uwagi,
                addresses,
                contacts,
                tradeCreditLimit);
        }
        public string? Patch(PatchPartyCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            using Uchwyt sfera = _sessionFactory.Create();
            using var partyBo = sfera.Podmioty().Znajdz(command.PartySignature);

            if (partyBo is null)
                return null;

            var isLocked = partyBo.Zablokuj();

            try
            {
                var party = partyBo.Dane;

                if (command.Signature.HasValue)
                    party.Sygnatura.PelnaSygnatura = command.Signature.Value!;

                if (command.DisplayName.HasValue)
                    party.NazwaSkrocona = command.DisplayName.Value!;

                if (command.IsActive.HasValue)
                    party.Aktywny = command.IsActive.Value;

                UpdatePerson(party, command);
                UpdateCompany(party, command);

                if (command.TaxId.HasValue)
                    party.NIP = command.TaxId.Value;

                if (command.EuTaxId.HasValue)
                {
                    party.NIPUE = command.EuTaxId.Value;
                    party.PodatnikUE = command.EuTaxId.Value is not null;
                }

                if (command.Notes.HasValue)
                    party.Uwagi = command.Notes.Value;

                if (command.PartyGroup.HasValue)
                    UpdatePartyGroup(sfera, party, command.PartyGroup.Value);

                if (command.Industries.HasValue)
                    UpdateIndustries(sfera, party, command.Industries.Value!);

                if (command.Features.HasValue)
                    UpdateFeatures(sfera, party, command.Features.Value!);

                if (!partyBo.Zapisz())
                {
                    throw new InvalidOperationException(BuildValidationMessage(
                        "Party update failed:",
                        partyBo.PobierzKomunikatyBledow()));
                }

                return party.Sygnatura.PelnaSygnatura;
            }
            finally
            {
                if (isLocked)
                    partyBo.Odblokuj();
            }
        }

        private static void UpdatePerson(Podmiot party, PatchPartyCommand command)
        {
            if (!command.FirstName.HasValue && !command.LastName.HasValue)
                return;

            var person = party.Osoba
                ?? throw new InvalidOperationException("First name and last name can only be updated for a person.");

            if (command.FirstName.HasValue)
                person.Imie = command.FirstName.Value;

            if (command.LastName.HasValue)
                person.Nazwisko = command.LastName.Value;
        }

        private static void UpdateCompany(Podmiot party, PatchPartyCommand command)
        {
            if (!command.CompanyName.HasValue &&
                !command.BusinessRegistryNumber.HasValue &&
                !command.NationalCourtRegisterNumber.HasValue)
            {
                return;
            }

            var company = party.Firma
                ?? throw new InvalidOperationException("Company data can only be updated for an organization.");

            if (command.CompanyName.HasValue)
                company.Nazwa = command.CompanyName.Value;

            if (command.BusinessRegistryNumber.HasValue)
                company.REGON = command.BusinessRegistryNumber.Value;

            if (command.NationalCourtRegisterNumber.HasValue)
                company.KRS = command.NationalCourtRegisterNumber.Value;
        }

        private static void UpdatePartyGroup(Uchwyt sfera, Podmiot party, string? groupName)
        {
            party.Grupy.Clear();

            if (groupName is null)
                return;

            using var group = sfera.Grupy().Znajdz(groupName)
                ?? throw new InvalidOperationException($"Party group '{groupName}' was not found.");

            party.Grupy.Add(group.Dane);
        }

        private static void UpdateIndustries(
            Uchwyt sfera,
            Podmiot party,
            IReadOnlyCollection<string> industryNames)
        {
            var industries = industryNames.Select(industryName =>
                sfera.Branze().Znajdz(industryName)
                ?? throw new InvalidOperationException($"Industry '{industryName}' was not found."))
                .ToList();

            party.Branze.Clear();

            foreach (var industry in industries)
            {
                using (industry)
                    party.Branze.Add(industry.Dane);
            }
        }

        private static void UpdateFeatures(
            Uchwyt sfera,
            Podmiot party,
            IReadOnlyCollection<string> featureNames)
        {
            var features = featureNames.Select(featureName =>
                sfera.Cechy().Znajdz(featureName)
                ?? throw new InvalidOperationException($"Feature '{featureName}' was not found."))
                .ToList();

            party.Cechy.Clear();

            foreach (var feature in features)
            {
                using (feature)
                    party.Cechy.Add(feature.Dane);
            }
        }

        private static string BuildValidationMessage(
            string messagePrefix,
            IEnumerable<KomunikatWalidacji> errors)
        {
            StringBuilder messageBuilder = new(messagePrefix);

            foreach (var error in errors)
            {
                var fieldNames = error.NazwyPol is null || !error.NazwyPol.Any()
                    ? "Unknown field"
                    : string.Join(", ", error.NazwyPol);

                messageBuilder.AppendLine();
                messageBuilder.Append($"{fieldNames}: {error.Tresc}");
            }

            return messageBuilder.ToString();
        }
        private DocumentTradeCreditLimitDto MapDocumentTradeCreditLimit(bool active, LimitKredytuKupieckiego? limit)
        {
            if (!active || limit is null)
                return new DocumentTradeCreditLimitDto(false, null, null, null);

            return new DocumentTradeCreditLimitDto(
                true,
                limit.Wartosc,
                limit.LimitPonizejWartosci,
                limit.LimitPowyzejWartosci);
        }
        #endregion
    }
}
