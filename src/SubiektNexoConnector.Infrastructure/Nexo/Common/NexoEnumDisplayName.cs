using System.Reflection;
using InsERT.Moria;

namespace SubiektNexoConnector.Infrastructure.Nexo.Common
{
    internal static class NexoEnumDisplayName
    {
        public static string Get<TEnum>(TEnum value) where TEnum : struct, Enum => Cache<TEnum>.Names.TryGetValue(value, out var name) ? name : value.ToString();

        private static class Cache<TEnum> where TEnum : struct, Enum
        {
            public static readonly IReadOnlyDictionary<TEnum, string> Names = Enum.GetValues<TEnum>().ToDictionary(value => value, GetName);

            private static string GetName(TEnum value)
            {
                var enumName = value.ToString();
                var field = typeof(TEnum).GetField(enumName);

                return field?.GetCustomAttribute<NazwaAttribute>()?.Value ?? enumName;
            }
        }
    }
}
