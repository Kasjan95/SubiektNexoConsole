using NSubstitute;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Nexo;

namespace SubiektNexoConnector.Api.Tests.Infrastructure;

public class NexoPartyRepositoryTests
{
    [Fact]
    public void GetDetails_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        var sessionFactory = Substitute.For<ISessionFactory>();
        var repository = new NexoPartyRepository(sessionFactory);

        Assert.Throws<ArgumentNullException>(() => repository.GetDetails(null!));
    }
}
