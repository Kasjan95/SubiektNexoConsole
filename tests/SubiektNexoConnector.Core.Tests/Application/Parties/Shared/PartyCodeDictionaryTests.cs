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
        [InlineData((short)1, (byte)0, "partner")]
        [InlineData((short)1, (byte)1, "employee")]
        [InlineData((short)1, (byte)3, "person")]
        [InlineData((short)2, (byte)4, "zus")]
        [InlineData((short)2, (byte)5, "tax-office")]
        [InlineData((short)2, (byte)6, "promotion-fund")]
        [InlineData((short)2, (byte)7, "company")]
        [InlineData((short)2, (byte)8, "financial-institution")]
        [InlineData((short)2, (byte)9, "cession-entity")]
        [InlineData((short)2, (byte)10, "other-institution")]
        [InlineData((short)2, (byte)11, "my-company")]
        [InlineData((short)2, (byte)12, "bailiff")]
        public void GetSubtypeName_ReturnsKnownSubtypeName(short type, byte subtype, string expectedName)
        {
            var result = PartyCodeDictionary.GetSubtypeName(type, subtype);

            Assert.Equal(expectedName, result);
        }

        [Fact]
        public void GetSubtypeName_ReturnsUnknown_WhenSubtypeIsNotMapped()
        {
            var result = PartyCodeDictionary.GetSubtypeName(2, 99);

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
