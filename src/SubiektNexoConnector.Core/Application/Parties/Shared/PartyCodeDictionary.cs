namespace SubiektNexoConnector.Core.Application.Parties.Shared
{
    public static class PartyCodeDictionary
    {
        public const string UnknownName = "unknown";

        private static readonly IReadOnlyDictionary<short, string> TypeNames =
            new Dictionary<short, string>
            {
                [1] = "person",
                [2] = "organization"
            };

        private static readonly IReadOnlyDictionary<int, string> CustomerStatusNames =
            new Dictionary<int, string>
            {
                [0] = "standard",
                [2] = "potential"
            };

        public static string GetTypeName(short type)
        {
            return TypeNames.TryGetValue(type, out var name)
                ? name
                : UnknownName;
        }

        public static string GetCustomerStatusName(int customerStatus)
        {
            return CustomerStatusNames.TryGetValue(customerStatus, out var name)
                ? name
                : UnknownName;
        }
    }
}
