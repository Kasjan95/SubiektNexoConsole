using Microsoft.Extensions.Logging;
using NSubstitute;
using SubiektNexoConnector.Infrastructure.Abstractions;
using SubiektNexoConnector.Infrastructure.Configuration;
using SubiektNexoConnector.Infrastructure.Nexo;

namespace SubiektNexoConnector.Api.Tests.Infrastructure;

public sealed class SferaExecutorTests
{
    [Fact]
    public async Task Execute_SerializesConcurrentOperations()
    {
        var sessionFactory = Substitute.For<ISessionFactory>();

        using var executor = new SferaExecutor(
            sessionFactory,
            new SferaExecutionOptions(),
            Substitute.For<ILogger<SferaExecutor>>());
        var firstOperationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeOperations = 0;
        var maximumActiveOperations = 0;

        Task ExecuteOperationAsync(bool waitForRelease) => Task.Run(() => executor.Execute(_ =>
        {
            var active = Interlocked.Increment(ref activeOperations);
            InterlockedExtensions.Max(ref maximumActiveOperations, active);

            if (waitForRelease)
            {
                firstOperationEntered.SetResult();
                releaseFirstOperation.Task.GetAwaiter().GetResult();
            }

            Interlocked.Decrement(ref activeOperations);
            return true;
        }));

        var first = ExecuteOperationAsync(waitForRelease: true);
        await firstOperationEntered.Task;
        var second = ExecuteOperationAsync(waitForRelease: false);

        await Task.Delay(100);
        Assert.Equal(1, maximumActiveOperations);

        releaseFirstOperation.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, maximumActiveOperations);
        sessionFactory.Received(2).Create();
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            var current = Volatile.Read(ref location);
            while (current < value)
            {
                var observed = Interlocked.CompareExchange(ref location, value, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }
}
