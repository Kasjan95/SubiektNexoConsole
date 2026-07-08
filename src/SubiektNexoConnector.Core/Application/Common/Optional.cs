using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubiektNexoConnector.Core.Application.Common
{
    [JsonConverter(typeof(OptionalJsonConverterFactory))]
    public readonly struct Optional<T>
    {
        public Optional(T? value)
        {
            HasValue = true;
            Value = value;
        }

        public bool HasValue { get; }

        public T? Value { get; }

        public T? GetValueOrDefault()
        {
            return Value;
        }

        public static implicit operator Optional<T>(T? value)
        {
            return new Optional<T>(value);
        }
    }

    public sealed class OptionalJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType
                && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var valueType = typeToConvert.GetGenericArguments()[0];
            var converterType = typeof(OptionalJsonConverter<>).MakeGenericType(valueType);

            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        private sealed class OptionalJsonConverter<TValue> : JsonConverter<Optional<TValue>>
        {
            public override Optional<TValue> Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                return new Optional<TValue>(value);
            }

            public override void Write(
                Utf8JsonWriter writer,
                Optional<TValue> value,
                JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value.Value, options);
            }
        }
    }
}
