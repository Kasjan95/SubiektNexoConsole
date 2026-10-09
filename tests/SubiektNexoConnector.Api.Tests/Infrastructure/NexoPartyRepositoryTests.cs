using InsERT.Moria.Sfera;
using NSubstitute;
using SubiektNexoConnector.Core.Application.Parties.CreateParty;
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

    [Fact]
    public void CreateParty_UsesSingleSferaExecutionLease()
    {
        var sferaExecutor = new CountingSferaExecutor();
        var repository = new NexoPartyRepository(sferaExecutor);
        var command = new CreatePartyCommand(
            "Test party",
            1,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            [],
            []);

        _ = repository.CreateParty(command);

        Assert.Equal(1, sferaExecutor.InvocationCount);
    }

    private sealed class CountingSferaExecutor : ISferaExecutor
    {
        public int InvocationCount { get; private set; }

        public T Execute<T>(
            Func<Uchwyt, T> operation,
            CancellationToken cancellationToken = default,
            string operationName = "",
            string sourceFilePath = "")
        {
            InvocationCount++;
            return default!;
        }

        public void Execute(
            Action<Uchwyt> operation,
            CancellationToken cancellationToken = default,
            string operationName = "",
            string sourceFilePath = "")
        {
            InvocationCount++;
        }
    }
}
