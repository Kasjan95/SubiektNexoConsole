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

        private static readonly IReadOnlyDictionary<(short Type, byte? Subtype), string> SubtypeNames =
            new Dictionary<(short Type, byte? Subtype), string>
            {
                [(1, 0)] = "partner",
                [(1, 1)] = "employee",
                [(1, 3)] = "person",
                [(2, 4)] = "zus",
                [(2, 5)] = "tax-office",
                [(2, 6)] = "promotion-fund",
                [(2, 7)] = "company",
                [(2, 8)] = "financial-institution",
                [(2, 9)] = "cession-entity",
                [(2, 10)] = "other-institution",
                [(2, 11)] = "my-company",
                [(2, 12)] = "bailiff"
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

        public static string GetSubtypeName(short type, byte? subtype)
        {
            return SubtypeNames.TryGetValue((type, subtype), out var name)
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
