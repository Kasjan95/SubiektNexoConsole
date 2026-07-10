using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Application.Parties.GetParties
{
    public sealed class GetPartiesHandler
    {
        private const int MaxPageSize = 1000;
        private readonly IPartyRepository _repository;

        public GetPartiesHandler(IPartyRepository repository)
        {
            _repository = repository;
        }

        public IReadOnlyCollection<PartyBasicDto> Handle(GetPartiesQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var parties = _repository.GetAll().AsEnumerable();

            parties = query.CustomerStatus switch
            {
                PartyCustomerStatusFilter.Standard => parties.Where(a => a.CustomerStatusName == "standard"),
                PartyCustomerStatusFilter.Potential => parties.Where(a => a.CustomerStatusName == "potential"),
                PartyCustomerStatusFilter.All => parties,
                _ => parties
            };

            if (query.Type.HasValue)
            {
                parties = parties.Where(a => a.Type == query.Type.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchTerm = query.Search.Trim();
                parties = parties.Where(a =>
                    Contains(a.TaxId, searchTerm) ||
                    Contains(a.Signature, searchTerm) ||
                    Contains(a.DisplayName, searchTerm) ||
                    Contains(a.CompanyName, searchTerm) ||
                    Contains(a.FirstName, searchTerm) ||
                    Contains(a.LastName, searchTerm));
            }

            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            return parties
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        private static bool Contains(string? value, string searchTerm)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
        }
    }
}
