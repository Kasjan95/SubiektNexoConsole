using System.Text.Json;
using SubiektNexoConnector.Core.Application.Common;

namespace SubiektNexoConnector.Core.Tests.Application.Common
{
    public class OptionalTests
    {
        private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void Deserialize_LeavesHasValueFalse_WhenPropertyIsMissing()
        {
            var result = JsonSerializer.Deserialize<PatchRequest>("{}", WebJsonOptions);

            Assert.NotNull(result);
            Assert.False(result!.Name.HasValue);
        }

        [Fact]
        public void Deserialize_SetsHasValueTrueAndValueNull_WhenPropertyIsNull()
        {
            var result = JsonSerializer.Deserialize<PatchRequest>(
                """
                { "ean": null }
                """,
                WebJsonOptions);

            Assert.NotNull(result);
            Assert.True(result!.Ean.HasValue);
            Assert.Null(result.Ean.Value);
        }

        [Fact]
        public void Deserialize_SetsHasValueTrueAndValue_WhenPropertyHasValue()
        {
            var result = JsonSerializer.Deserialize<PatchRequest>(
                """
                { "sku": "PROD-001" }
                """,
                WebJsonOptions);

            Assert.NotNull(result);
            Assert.True(result!.Sku.HasValue);
            Assert.Equal("PROD-001", result.Sku.Value);
        }

        private sealed record PatchRequest(
            Optional<string> Name,
            Optional<string?> Ean,
            Optional<string> Sku);
    }
}
