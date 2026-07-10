using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.GetParties
{
    public class GetPartiesHandlerTests
    {
        [Fact]
        public void Handle_ReturnsOnlyStandardParties_ByDefault()
        {
            var repository = Substitute.For<IPartyRepository>();
            repository.GetAll().Returns(
            [
                CreateParty("STD", "Standard Party", customerStatus: 0, customerStatusName: "standard"),
                CreateParty("POT", "Potential Party", customerStatus: 2, customerStatusName: "potential")
            ]);
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(new GetPartiesQuery());

            Assert.Single(result);
            Assert.Equal("STD", result.Single().Signature);
            repository.Received(1).GetAll();
        }

        [Fact]
        public void Handle_ReturnsPotentialParties_WhenRequested()
        {
            var repository = Substitute.For<IPartyRepository>();
            repository.GetAll().Returns(
            [
                CreateParty("STD", "Standard Party", customerStatus: 0, customerStatusName: "standard"),
                CreateParty("POT", "Potential Party", customerStatus: 2, customerStatusName: "potential")
            ]);
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(new GetPartiesQuery { CustomerStatus = PartyCustomerStatusFilter.Potential });

            Assert.Single(result);
            Assert.Equal("POT", result.Single().Signature);
        }

        [Fact]
        public void Handle_ReturnsAllParties_WhenRequested()
        {
            var repository = Substitute.For<IPartyRepository>();
            repository.GetAll().Returns(
            [
                CreateParty("STD", "Standard Party", customerStatus: 0, customerStatusName: "standard"),
                CreateParty("POT", "Potential Party", customerStatus: 2, customerStatusName: "potential")
            ]);
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(new GetPartiesQuery { CustomerStatus = PartyCustomerStatusFilter.All });

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Handle_FiltersByType_WhenProvided()
        {
            var repository = Substitute.For<IPartyRepository>();
            repository.GetAll().Returns(
            [
                CreateParty("EMP", "Employee", type: 1, subtype: 1),
                CreateParty("CMP", "Company", type: 2, subtype: 7),
                CreateParty("BANK", "Bank", type: 2, subtype: 8)
            ]);
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(new GetPartiesQuery
            {
                CustomerStatus = PartyCustomerStatusFilter.All,
                Type = 2
            });

            Assert.Equal(2, result.Count);
            Assert.Contains(result, a => a.Signature == "CMP");
            Assert.Contains(result, a => a.Signature == "BANK");
        }

        [Theory]
        [InlineData("1234567890")]
        [InlineData("cmp")]
        [InlineData("alpha")]
        [InlineData("jan")]
        [InlineData("kow")]
        public void Handle_FiltersBySearchTerm_AcrossSupportedFields(string searchTerm)
        {
            var repository = Substitute.For<IPartyRepository>();
            repository.GetAll().Returns(
            [
                CreateParty(
                    signature: "CMP",
                    displayName: "Alpha Company",
                    taxId: "1234567890",
                    firstName: "Jan",
                    lastName: "Kowalski",
                    companyName: "Alpha sp. z o.o."),
                CreateParty(
                    signature: "OTHER",
                    displayName: "Beta Company",
                    taxId: "9999999999",
                    firstName: "Anna",
                    lastName: "Nowak",
                    companyName: "Beta sp. z o.o.")
            ]);
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(new GetPartiesQuery
            {
                CustomerStatus = PartyCustomerStatusFilter.All,
                Search = searchTerm
            });

            Assert.Single(result);
            Assert.Equal("CMP", result.Single().Signature);
        }

        [Fact]
        public void Handle_ReturnsRequestedPage()
        {
            var repository = Substitute.For<IPartyRepository>();
            repository.GetAll().Returns(
            [
                CreateParty("P1", "Party 1"),
                CreateParty("P2", "Party 2"),
                CreateParty("P3", "Party 3")
            ]);
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(new GetPartiesQuery
            {
                CustomerStatus = PartyCustomerStatusFilter.All,
                Page = 2,
                PageSize = 1
            });

            Assert.Single(result);
            Assert.Equal("P2", result.Single().Signature);
        }

        [Fact]
        public void Handle_UsesDefaultPage_WhenPageIsLessThanOne()
        {
            var repository = Substitute.For<IPartyRepository>();
            repository.GetAll().Returns(
            [
                CreateParty("P1", "Party 1"),
                CreateParty("P2", "Party 2")
            ]);
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(new GetPartiesQuery
            {
                CustomerStatus = PartyCustomerStatusFilter.All,
                Page = 0,
                PageSize = 1
            });

            Assert.Single(result);
            Assert.Equal("P1", result.Single().Signature);
        }

        [Fact]
        public void Handle_LimitsPageSize_ToMaximum()
        {
            var repository = Substitute.For<IPartyRepository>();
            repository.GetAll().Returns(
                Enumerable.Range(1, 1200)
                    .Select(i => CreateParty($"P{i}", $"Party {i}"))
                    .ToList());
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(new GetPartiesQuery
            {
                CustomerStatus = PartyCustomerStatusFilter.All,
                PageSize = 5000
            });

            Assert.Equal(1000, result.Count);
        }

        private static PartyBasicDto CreateParty(
            string signature,
            string displayName,
            short type = 2,
            byte? subtype = 7,
            int customerStatus = 0,
            string customerStatusName = "standard",
            string taxId = "",
            bool isActive = true,
            string? firstName = null,
            string? lastName = null,
            string? companyName = null)
        {
            return new PartyBasicDto(
                signature,
                displayName,
                type,
                subtype,
                "type-name",
                "subtype-name",
                customerStatus,
                customerStatusName,
                taxId,
                isActive,
                firstName,
                lastName,
                companyName);
        }
    }
}
