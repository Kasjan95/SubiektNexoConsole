using NSubstitute;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Nexo;

namespace SubiektNexoConnector.Api.Tests.Infrastructure;

public class NexoPartyRepositoryTests
{
    [Fact]
    public void GetDetails_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        var sferaExecutor = Substitute.For<ISferaExecutor>();
        var repository = new NexoPartyRepository(sferaExecutor);

        Assert.Throws<ArgumentNullException>(() => repository.GetDetailsParty(null!));
    }
}
