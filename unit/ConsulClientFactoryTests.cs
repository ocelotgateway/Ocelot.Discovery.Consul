using Consul;

namespace Ocelot.Discovery.Consul.UnitTests;

public class ConsulClientFactoryTests : UnitTest
{
    private readonly ConsulClientFactory _sut = new();

    [Fact]
    public void Get_WithValidConfig_ReturnsConsulClient()
    {
        // Arrange
        var config = new ConsulRegistryConfiguration("http", "localhost", 8500, "svc", null);

        // Act
        var result = _sut.Get(config);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ConsulClient>(result);
    }

    [Fact]
    public void Get_WithToken_SetsClientToken()
    {
        // Arrange
        const string token = "my-secret-token";
        var config = new ConsulRegistryConfiguration("http", "localhost", 8500, "svc", token);

        // Act
        var result = _sut.Get(config) as ConsulClient;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(token, result.Config.Token);
    }

    [Fact]
    public void Get_WithoutToken_ClientTokenIsNotSet()
    {
        // Arrange
        var config = new ConsulRegistryConfiguration("http", "localhost", 8500, "svc", null);

        // Act
        var result = _sut.Get(config) as ConsulClient;

        // Assert
        Assert.NotNull(result);
        Assert.True(string.IsNullOrEmpty(result.Config.Token));
    }

    [Fact]
    public void Get_WithEmptyToken_ClientTokenIsNotSet()
    {
        // Arrange
        var config = new ConsulRegistryConfiguration("http", "localhost", 8500, "svc", string.Empty);

        // Act
        var result = _sut.Get(config) as ConsulClient;

        // Assert
        Assert.NotNull(result);
        Assert.True(string.IsNullOrEmpty(result.Config.Token));
    }

    [Fact]
    public void Get_WithSchemeHostPort_SetsClientAddress()
    {
        // Arrange
        var config = new ConsulRegistryConfiguration("http", "consulhost", 8501, "svc", null);

        // Act
        var result = _sut.Get(config) as ConsulClient;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new Uri("http://consulhost:8501"), result.Config.Address);
    }
}
