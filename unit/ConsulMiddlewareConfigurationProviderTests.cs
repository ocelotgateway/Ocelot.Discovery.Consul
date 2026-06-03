using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Ocelot.Cache;
using Ocelot.Configuration;
using Ocelot.Configuration.Creator;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;
using Ocelot.Errors;
using Ocelot.Logging;
using Ocelot.Responses;
using System.Text;

namespace Ocelot.Discovery.Consul.UnitTests;

public class ConsulMiddlewareConfigurationProviderTests : UnitTest
{
    private readonly Mock<IOcelotCache<FileConfiguration>> _cache;
    private readonly Mock<IConsulClientFactory> _consulClientFactory;
    private readonly Mock<IConsulClient> _consulClient;
    private readonly Mock<IKVEndpoint> _kvEndpoint;
    private readonly Mock<IOcelotLoggerFactory> _loggerFactory;
    private readonly Mock<IOptionsMonitor<FileConfiguration>> _fileConfigMonitor;
    private readonly Mock<IInternalConfigurationCreator> _internalConfigCreator;
    private readonly Mock<IInternalConfigurationRepository> _internalConfigRepo;
    private readonly Mock<IApplicationBuilder> _appBuilder;
    private readonly Mock<IServiceProvider> _serviceProvider;

    public ConsulMiddlewareConfigurationProviderTests()
    {
        _kvEndpoint = new Mock<IKVEndpoint>();
        _consulClient = new Mock<IConsulClient>();
        _consulClient.SetupGet(x => x.KV).Returns(_kvEndpoint.Object);
        _consulClientFactory = new Mock<IConsulClientFactory>();
        _consulClientFactory.Setup(x => x.Get(It.IsAny<ConsulRegistryConfiguration>())).Returns(_consulClient.Object);

        _cache = new Mock<IOcelotCache<FileConfiguration>>();
        var logger = new Mock<IOcelotLogger>();
        _loggerFactory = new Mock<IOcelotLoggerFactory>();
        _loggerFactory.Setup(x => x.CreateLogger<ConsulFileConfigurationRepository>()).Returns(logger.Object);

        _fileConfigMonitor = new Mock<IOptionsMonitor<FileConfiguration>>();
        _internalConfigCreator = new Mock<IInternalConfigurationCreator>();
        _internalConfigRepo = new Mock<IInternalConfigurationRepository>();

        _serviceProvider = new Mock<IServiceProvider>();
        _appBuilder = new Mock<IApplicationBuilder>();
        _appBuilder.SetupGet(x => x.ApplicationServices).Returns(_serviceProvider.Object);
    }

    [Fact]
    public async Task Get_WhenNotUsingConsulRepo_DoesNothing()
    {
        // Arrange: register a non-Consul IFileConfigurationRepository implementation
        var mockRepo = new Mock<IFileConfigurationRepository>();
        _serviceProvider.Setup(x => x.GetService(typeof(IFileConfigurationRepository))).Returns(mockRepo.Object);

        // Act
        await ConsulMiddlewareConfigurationProvider.Get(_appBuilder.Object);

        // Assert: no calls were made to retrieve or store config
        mockRepo.Verify(x => x.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockRepo.Verify(x => x.SetAsync(It.IsAny<FileConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_WhenUsingConsulRepo_AndConfigIsNullInConsul_SetsFileConfigInConsul()
    {
        // Arrange: consul has no config (KV returns null response)
        var consulRepo = GivenConsulFileConfigurationRepository();
        _cache.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>())).Returns((FileConfiguration)null);
        _kvEndpoint.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<KVPair>());

        // SetAsync will be called with CurrentValue - make KV.Put succeed
        _kvEndpoint.Setup(x => x.Put(It.IsAny<KVPair>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WriteResult<bool> { Response = true });

        var currentValue = new FileConfiguration();
        _fileConfigMonitor.SetupGet(x => x.CurrentValue).Returns(currentValue);

        GivenServicesAreRegistered(consulRepo);

        // Act
        await ConsulMiddlewareConfigurationProvider.Get(_appBuilder.Object);

        // Assert: KV.Put was called to store the current file config
        _kvEndpoint.Verify(x => x.Put(It.IsAny<KVPair>(), It.IsAny<CancellationToken>()), Times.Once);
        _internalConfigCreator.Verify(x => x.Create(It.IsAny<FileConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task Get_WhenUsingConsulRepo_AndConfigExists_SetsInternalConfig()
    {
        // Arrange: consul returns a valid FileConfiguration
        var consulRepo = GivenConsulFileConfigurationRepository();
        var storedConfig = new FileConfiguration();
        GivenConsulReturnsConfig(storedConfig);

        var internalConfig = new Mock<IInternalConfiguration>();
        var creatorResponse = new OkResponse<IInternalConfiguration>(internalConfig.Object);
        _internalConfigCreator.Setup(x => x.Create(It.IsAny<FileConfiguration>()))
            .ReturnsAsync(creatorResponse);
        _internalConfigRepo.Setup(x => x.AddOrReplace(It.IsAny<IInternalConfiguration>())).Returns(string.Empty);

        GivenServicesAreRegistered(consulRepo);

        // Act
        await ConsulMiddlewareConfigurationProvider.Get(_appBuilder.Object);

        // Assert: internal config was created and stored
        _internalConfigCreator.Verify(x => x.Create(It.IsAny<FileConfiguration>()), Times.Once);
        _internalConfigRepo.Verify(x => x.AddOrReplace(It.IsAny<IInternalConfiguration>()), Times.Once);
    }

    [Fact]
    public async Task Get_WhenUsingConsulRepo_AndCreatorReturnsError_ThrowsException()
    {
        // Arrange: consul returns a config, but creator reports error
        var consulRepo = GivenConsulFileConfigurationRepository();
        var storedConfig = new FileConfiguration();
        GivenConsulReturnsConfig(storedConfig);

        var errorResponse = new ErrorResponse<IInternalConfiguration>(new UnknownError("creator failed"));
        _internalConfigCreator.Setup(x => x.Create(It.IsAny<FileConfiguration>()))
            .ReturnsAsync(errorResponse);

        GivenServicesAreRegistered(consulRepo);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            ConsulMiddlewareConfigurationProvider.Get(_appBuilder.Object));
    }

    private ConsulFileConfigurationRepository GivenConsulFileConfigurationRepository()
    {
        var fileConfig = new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration
            {
                ServiceDiscoveryProvider = new FileServiceDiscoveryProvider
                {
                    Scheme = "http",
                    Host = "localhost",
                    Port = 8500,
                },
            },
        };
        var options = new Mock<IOptions<FileConfiguration>>();
        options.SetupGet(x => x.Value).Returns(fileConfig);
        return new ConsulFileConfigurationRepository(options.Object, _cache.Object, _consulClientFactory.Object, _loggerFactory.Object);
    }

    private void GivenConsulReturnsConfig(FileConfiguration config)
    {
        _cache.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>())).Returns((FileConfiguration)null);
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        var bytes = Encoding.UTF8.GetBytes(json);
        var kvPair = new KVPair("InternalConfiguration") { Value = bytes };
        var queryResult = new QueryResult<KVPair> { Response = kvPair };
        _kvEndpoint.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);
    }

    private void GivenServicesAreRegistered(ConsulFileConfigurationRepository consulRepo)
    {
        _serviceProvider.Setup(x => x.GetService(typeof(IFileConfigurationRepository))).Returns(consulRepo);
        _serviceProvider.Setup(x => x.GetService(typeof(IOptionsMonitor<FileConfiguration>))).Returns(_fileConfigMonitor.Object);
        _serviceProvider.Setup(x => x.GetService(typeof(IInternalConfigurationCreator))).Returns(_internalConfigCreator.Object);
        _serviceProvider.Setup(x => x.GetService(typeof(IInternalConfigurationRepository))).Returns(_internalConfigRepo.Object);
    }
}
