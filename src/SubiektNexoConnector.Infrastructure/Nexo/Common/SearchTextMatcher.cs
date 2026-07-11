using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Infrastructure.Nexo.Common
{
    public static class SearchTextMatcher
    {
        public static bool MatchesSearch(ISearchable value, string searchTerm)
        {
            ArgumentNullException.ThrowIfNull(value);

            return value.GetSearchText().Contains(
                searchTerm,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
