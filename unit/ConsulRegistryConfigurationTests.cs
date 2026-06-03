namespace Ocelot.Discovery.Consul.UnitTests;

public class ConsulRegistryConfigurationTests : UnitTest
{
    [Fact]
    public void Ctor_DefaultCtor_CreatesInstance()
    {
        // Act
        var sut = new ConsulRegistryConfiguration();

        // Assert
        Assert.NotNull(sut);
        Assert.Null(sut.Host);
        Assert.Null(sut.Scheme);
        Assert.Null(sut.KeyOfServiceInConsul);
        Assert.Null(sut.Token);
        Assert.Equal(0, sut.Port);
    }

    [Fact]
    public void Ctor_WithEmptyHost_DefaultsToLocalhost()
    {
        // Act
        var sut = new ConsulRegistryConfiguration(null, null, 8500, "svc", null);

        // Assert
        Assert.Equal("localhost", sut.Host);
    }

    [Fact]
    public void Ctor_WithEmptyScheme_DefaultsToHttp()
    {
        // Act
        var sut = new ConsulRegistryConfiguration(null, "localhost", 8500, "svc", null);

        // Assert
        Assert.Equal(Uri.UriSchemeHttp, sut.Scheme);
    }

    [Fact]
    public void Ctor_WithZeroPort_DefaultsTo8500()
    {
        // Act
        var sut = new ConsulRegistryConfiguration("http", "localhost", 0, "svc", null);

        // Assert
        Assert.Equal(ConsulRegistryConfiguration.DefaultHttpPort, sut.Port);
    }

    [Fact]
    public void Ctor_WithNegativePort_DefaultsTo8500()
    {
        // Act
        var sut = new ConsulRegistryConfiguration("http", "localhost", -1, "svc", null);

        // Assert
        Assert.Equal(ConsulRegistryConfiguration.DefaultHttpPort, sut.Port);
    }

    [Fact]
    public void Ctor_WithValidParams_PropertiesSetCorrectly()
    {
        // Act
        var sut = new ConsulRegistryConfiguration("https", "myhost", 9500, "my-service", "secret-token");

        // Assert
        Assert.Equal("https", sut.Scheme);
        Assert.Equal("myhost", sut.Host);
        Assert.Equal(9500, sut.Port);
        Assert.Equal("my-service", sut.KeyOfServiceInConsul);
        Assert.Equal("secret-token", sut.Token);
    }

    [Fact]
    public void Ctor_WithNullToken_TokenIsNull()
    {
        // Act
        var sut = new ConsulRegistryConfiguration("http", "localhost", 8500, "svc", null);

        // Assert
        Assert.Null(sut.Token);
    }

    [Fact]
    public void DefaultHttpPort_ConstantValue_Is8500()
    {
        Assert.Equal(8500, ConsulRegistryConfiguration.DefaultHttpPort);
    }
}
