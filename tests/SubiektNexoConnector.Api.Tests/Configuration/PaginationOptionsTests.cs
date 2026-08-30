using SubiektNexoConnector.Api.Configuration;

namespace SubiektNexoConnector.Api.Tests.Configuration;

public class PaginationOptionsTests
{
    private readonly PaginationOptions _options = new()
    {
        DefaultPageSize = 25,
        MaxPageSize = 50
    };

    [Fact]
    public void TryResolve_UsesConfiguredDefaults_WhenParametersAreMissing()
    {
        var valid = _options.TryResolve(null, null, out var parameters, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
        Assert.Equal(new PaginationParameters(1, 25), parameters);
    }

    [Theory]
    [InlineData(0, 10, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, 51, "pageSize")]
    [InlineData(2147483647, 2, "page")]
    public void TryResolve_ReturnsValidationError_ForValuesOutsideContract(int page, int pageSize, string errorKey)
    {
        var valid = _options.TryResolve(page, pageSize, out _, out var errors);

        Assert.False(valid);
        Assert.Contains(errorKey, errors.Keys);
    }
}
