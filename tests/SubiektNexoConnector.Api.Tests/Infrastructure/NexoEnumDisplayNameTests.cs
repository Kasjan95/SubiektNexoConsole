using InsERT.Moria.Klienci;
using SubiektNexoConnector.Infrastructure.Nexo.Common;

namespace SubiektNexoConnector.Api.Tests.Infrastructure;

public sealed class NexoEnumDisplayNameTests
{
    [Theory]
    [InlineData(PodtypPodmiotu.InstytucjaFinansowa, "Instytucja finansowa")]
    [InlineData(PodtypPodmiotu.Wspolnik, "Wspólnik")]
    public void Get_ReturnsNameProvidedByNexo(PodtypPodmiotu value, string expected)
    {
        Assert.Equal(expected, NexoEnumDisplayName.Get(value));
    }

    [Theory]
    [InlineData(StatusKlienta.Standardowy, "Standardowy")]
    [InlineData(StatusKlienta.Potencjalny, "Potencjalny")]
    public void Get_ReturnsCustomerStatusNameProvidedByNexo(StatusKlienta value, string expected)
    {
        Assert.Equal(expected, NexoEnumDisplayName.Get(value));
    }
}
