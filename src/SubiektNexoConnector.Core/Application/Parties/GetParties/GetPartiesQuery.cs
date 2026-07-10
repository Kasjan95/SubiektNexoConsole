namespace SubiektNexoConnector.Core.Application.Parties.GetParties
{
    public enum PartyCustomerStatusFilter
    {
        Standard,
        Potential,
        All
    }

    public sealed class GetPartiesQuery
    {
        public PartyCustomerStatusFilter CustomerStatus { get; init; } = PartyCustomerStatusFilter.Standard;
        public short? Type { get; init; }
        public string? Search { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 100;
    }
}
