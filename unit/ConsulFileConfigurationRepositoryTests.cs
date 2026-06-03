using Consul;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Ocelot.Cache;
using Ocelot.Configuration.File;
using Ocelot.Logging;
using System.Text;

namespace Ocelot.Discovery.Consul.UnitTests;

public class ConsulFileConfigurationRepositoryTests : UnitTest
{
    private ConsulFileConfigurationRepository _repo;
    private readonly Mock<IOptions<FileConfiguration>> _options;
    private readonly Mock<IOcelotCache<FileConfiguration>> _cache;
    private readonly Mock<IConsulClientFactory> _factory;
    private readonly Mock<IOcelotLoggerFactory> _loggerFactory;
    private readonly Mock<IConsulClient> _client;
    private readonly Mock<IKVEndpoint> _kvEndpoint;
    private FileConfiguration _fileConfiguration;
    private FileConfiguration _getResult;

    public ConsulFileConfigurationRepositoryTests()
    {
        _cache = new Mock<IOcelotCache<FileConfiguration>>();
        _loggerFactory = new Mock<IOcelotLoggerFactory>();

        _options = new Mock<IOptions<FileConfiguration>>();
        _factory = new Mock<IConsulClientFactory>();
        _client = new Mock<IConsulClient>();
        _kvEndpoint = new Mock<IKVEndpoint>();
        _client
            .Setup(x => x.KV)
            .Returns(_kvEndpoint.Object);
        _factory
            .Setup(x => x.Get(It.IsAny<ConsulRegistryConfiguration>()))
            .Returns(_client.Object);
        _options
            .SetupGet(x => x.Value)
            .Returns(() => _fileConfiguration);
    }

    [Fact]
    public async Task Should_set_config()
    {
        // Arrange
        var config = GivenFakeFileConfiguration();
        GivenWritingToConsulSucceeds();

        // Act
        await _repo.SetAsync(config, CancelMe);

        // Assert
        ThenTheConfigurationIsStoredAs(config);
    }

    [Fact]
    public async Task Should_get_config()
    {
        // Arrange
        var config = _fileConfiguration = GivenFakeFileConfiguration();
        GivenFetchFromConsulSucceeds();

        // Act
        _getResult = await _repo.GetAsync(CancelMe);

        // Assert
        ThenTheConfigurationIs(config);
    }

    [Fact]
    public async Task Should_get_null_config()
    {
        // Arrange
        _fileConfiguration = GivenFakeFileConfiguration();
        GivenFetchFromConsulReturnsNull();

        // Act
        _getResult = await _repo.GetAsync(CancelMe);

        // Assert
        Assert.Null(_getResult);
    }

    [Fact]
    public async Task Should_get_config_from_cache()
    {
        // Arrange
        var config = _fileConfiguration = GivenFakeFileConfiguration();
        GivenFetchFromCacheSucceeds();

        // Act
        _getResult = await _repo.GetAsync(CancelMe);

        // Assert
        ThenTheConfigurationIs(config);
    }

    [Fact]
    public async Task Should_set_config_key()
    {
        // Arrange
        _fileConfiguration = GivenFakeFileConfiguration();
        GivenTheConfigKeyComesFromFileConfig("Tom");
        GivenFetchFromConsulSucceeds();

        // Act
        _getResult = await _repo.GetAsync(CancelMe);

        // Assert
        ThenTheConfigKeyIs("Tom");
    }

    [Fact]
    public async Task Should_set_default_config_key()
    {
        // Arrange
        _fileConfiguration = GivenFakeFileConfiguration();
        GivenFetchFromConsulSucceeds();

        // Act
        _getResult = await _repo.GetAsync(CancelMe);

        // Assert
        ThenTheConfigKeyIs("InternalConfiguration");
    }

    private void ThenTheConfigKeyIs(string expected)
    {
        _kvEndpoint.Verify(x => x.Get(expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void GivenTheConfigKeyComesFromFileConfig(string key)
    {
        _fileConfiguration.GlobalConfiguration.ServiceDiscoveryProvider.ConfigurationKey = key;
        _repo = new ConsulFileConfigurationRepository(_options.Object, _cache.Object, _factory.Object, _loggerFactory.Object);
    }

    private void ThenTheConfigurationIs(FileConfiguration config)
    {
        var expected = JsonConvert.SerializeObject(config, Formatting.Indented);
        var result = JsonConvert.SerializeObject(_getResult, Formatting.Indented);
        Assert.Equal(expected, result);
    }

    private void GivenWritingToConsulSucceeds()
    {
        var response = new WriteResult<bool>
        {
            Response = true,
        };
        _kvEndpoint.Setup(x => x.Put(It.IsAny<KVPair>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
    }

    private void GivenFetchFromCacheSucceeds()
    {
        _cache.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>())).Returns(_fileConfiguration);
    }

    private void GivenFetchFromConsulReturnsNull()
    {
        var result = new QueryResult<KVPair>();
        _kvEndpoint.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private void GivenFetchFromConsulSucceeds()
    {
        var json = JsonConvert.SerializeObject(_fileConfiguration, Formatting.Indented);
        var bytes = Encoding.UTF8.GetBytes(json);
        var kvp = new KVPair("OcelotConfiguration")
        {
            Value = bytes,
        };
        var query = new QueryResult<KVPair>
        {
            Response = kvp,
        };
        _kvEndpoint.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(query);
    }

    [Fact]
    public async Task SetAsync_WhenPutFails_ThrowsConsulConfigurationRepositoryException()
    {
        // Arrange
        var config = GivenFakeFileConfiguration();
        GivenWritingToConsulFails();

        // Act & Assert
        await Assert.ThrowsAsync<ConsulConfigurationRepositoryException>(() => _repo.SetAsync(config, CancelMe));
    }

    [Fact]
    public void Get_WhenCalled_ReturnsConfiguration()
    {
        // Arrange
        var config = _fileConfiguration = GivenFakeFileConfiguration();
        GivenFetchFromConsulSucceeds();

        // Act
        var result = _repo.Get();

        // Assert
        var expected = JsonConvert.SerializeObject(config, Formatting.Indented);
        var actual = JsonConvert.SerializeObject(result, Formatting.Indented);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Set_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var config = GivenFakeFileConfiguration();
        GivenWritingToConsulSucceeds();

        // Act & Assert: sync Set should not throw
        var exception = Record.Exception(() => _repo.Set(config));
        Assert.Null(exception);
    }

    private void GivenWritingToConsulFails()
    {
        var response = new WriteResult<bool> { Response = false };
        _kvEndpoint.Setup(x => x.Put(It.IsAny<KVPair>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
    }

    private void ThenTheConfigurationIsStoredAs(FileConfiguration config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        var bytes = Encoding.UTF8.GetBytes(json);
        _kvEndpoint.Verify(x => x.Put(It.Is<KVPair>(k => k.Value.SequenceEqual(bytes)), It.IsAny<CancellationToken>()), Times.Once);
    }

    private FileConfiguration GivenFakeFileConfiguration()
    {
        var routes = new List<FileRoute>
        {
            new()
            {
                DownstreamHostAndPorts = [ new("123.12.12.12", 80) ],
                DownstreamScheme = Uri.UriSchemeHttps,
                DownstreamPathTemplate = "/asdfs/test/{test}",
            },
        };
        var globalConfiguration = new FileGlobalConfiguration
        {
            ServiceDiscoveryProvider = new FileServiceDiscoveryProvider
            {
                Scheme = Uri.UriSchemeHttps,
                Port = 198,
                Host = "blah",
            },
        };
        _fileConfiguration = new FileConfiguration
        {
            GlobalConfiguration = globalConfiguration,
            Routes = routes,
        };
        _repo = new ConsulFileConfigurationRepository(_options.Object, _cache.Object, _factory.Object, _loggerFactory.Object);
        return _fileConfiguration;
    }
}
