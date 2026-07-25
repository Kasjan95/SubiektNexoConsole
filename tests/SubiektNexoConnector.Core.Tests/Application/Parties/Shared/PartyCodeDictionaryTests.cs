using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.Shared
{
    public class PartyCodeDictionaryTests
    {
        [Theory]
        [InlineData((short)1, "person")]
        [InlineData((short)2, "organization")]
        public void GetTypeName_ReturnsKnownTypeName(short type, string expectedName)
        {
            var result = PartyCodeDictionary.GetTypeName(type);

            Assert.Equal(expectedName, result);
        }

        [Fact]
        public void GetTypeName_ReturnsUnknown_WhenTypeIsNotMapped()
        {
            var result = PartyCodeDictionary.GetTypeName(99);

            Assert.Equal(PartyCodeDictionary.UnknownName, result);
        }

        [Theory]
        [InlineData(0, "standard")]
        [InlineData(2, "potential")]
        public void GetCustomerStatusName_ReturnsKnownStatusName(int customerStatus, string expectedName)
        {
            var result = PartyCodeDictionary.GetCustomerStatusName(customerStatus);

            Assert.Equal(expectedName, result);
        }

        [Fact]
        public void GetCustomerStatusName_ReturnsUnknown_WhenStatusIsNotMapped()
        {
            var result = PartyCodeDictionary.GetCustomerStatusName(99);

            Assert.Equal(PartyCodeDictionary.UnknownName, result);
        }
    }
}
