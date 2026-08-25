using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties.GetParties;
using SubiektNexoConnector.Core.Application.Parties.Shared;

namespace SubiektNexoConnector.Core.Tests.Application.Parties.GetParties
{
    public class GetPartiesHandlerTests
    {
        [Fact]
        public void Handle_ReturnsPartiesFromRepository()
        {
            var repository = Substitute.For<IPartyRepository>();
            var expectedParties = new List<PartyBasicDto>
            {
                new("SZEF", "Kamil Kasjaniuk", 1, 1, "person", "employee", 0, "standard", null, true, "Kamil", "Kasjaniuk", null),
                new("FIRMA", "MULTIPROJEKT Kamil Kasjaniuk", 2, 11, "company", "ownCompany", 0, "standard", "6372205552", true, null, null, "MULTIPROJEKT Kamil Kasjaniuk")
            };
            var query = new GetPartiesQuery
            {
                CustomerStatus = PartyCustomerStatusFilter.Standard,
                Type = 2,
                Search = "multi",
                Page = 2,
                PageSize = 50
            };

            repository.GetAllParties(query).Returns(expectedParties);
            var handler = new GetPartiesHandler(repository);

            var result = handler.Handle(query);

            Assert.Equal(expectedParties, result);
            repository.Received(1).GetAllParties(query);
        }

        [Fact]
        public void Handle_ThrowsArgumentNullException_WhenQueryIsNull()
        {
            var repository = Substitute.For<IPartyRepository>();
            var handler = new GetPartiesHandler(repository);

            Assert.Throws<ArgumentNullException>(() => handler.Handle(null!));
        }
    }
}
