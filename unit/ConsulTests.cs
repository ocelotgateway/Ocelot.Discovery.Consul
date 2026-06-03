using Consul;
using Ocelot.Logging;
using Ocelot.ServiceDiscovery.Providers;
using Ocelot.Values;

namespace Ocelot.Discovery.Consul.UnitTests;

public class ConsulTests : UnitTest
{
    private readonly Mock<IConsulClientFactory> _clientFactory;
    private readonly Mock<IConsulClient> _consulClient;
    private readonly Mock<IOcelotLoggerFactory> _loggerFactory;
    private readonly Mock<IOcelotLogger> _logger;
    private readonly Mock<IConsulServiceBuilder> _serviceBuilder;
    private readonly Mock<IHealthEndpoint> _healthEndpoint;
    private readonly Mock<ICatalogEndpoint> _catalogEndpoint;
    private readonly ConsulRegistryConfiguration _configuration;
    private readonly Consul _sut;

    public ConsulTests()
    {
        _clientFactory = new Mock<IConsulClientFactory>();
        _consulClient = new Mock<IConsulClient>();
        _loggerFactory = new Mock<IOcelotLoggerFactory>();
        _logger = new Mock<IOcelotLogger>();
        _serviceBuilder = new Mock<IConsulServiceBuilder>();
        _healthEndpoint = new Mock<IHealthEndpoint>();
        _catalogEndpoint = new Mock<ICatalogEndpoint>();

        _loggerFactory.Setup(x => x.CreateLogger<Consul>()).Returns(_logger.Object);
        _clientFactory.Setup(x => x.Get(It.IsAny<ConsulRegistryConfiguration>())).Returns(_consulClient.Object);
        _consulClient.SetupGet(x => x.Health).Returns(_healthEndpoint.Object);
        _consulClient.SetupGet(x => x.Catalog).Returns(_catalogEndpoint.Object);

        _configuration = new ConsulRegistryConfiguration("http", "localhost", 8500, "test-service", null);
        _sut = new Consul(_configuration, _loggerFactory.Object, _clientFactory.Object, _serviceBuilder.Object);
    }

    [Fact]
    public async Task GetAsync_WhenNoEntriesFound_ReturnsEmptyList()
    {
        // Arrange
        GivenConsulHealthReturnsEntries(Array.Empty<ServiceEntry>());
        GivenConsulCatalogReturnsNodes(Array.Empty<Node>());

        // Act
        var result = await _sut.GetAsync();

        // Assert
        Assert.Empty(result);
        _logger.Verify(x => x.LogWarning(It.IsAny<Func<string>>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenNullEntriesResponse_ReturnsEmptyList()
    {
        // Arrange
        GivenConsulHealthReturnsEntries(null);
        GivenConsulCatalogReturnsNodes(Array.Empty<Node>());

        // Act
        var result = await _sut.GetAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAsync_WhenEntriesFound_DelegatesToServiceBuilder()
    {
        // Arrange
        var entries = new ServiceEntry[]
        {
            new() { Service = new AgentService { Service = "test-service", Address = "10.0.0.1", Port = 8080 } },
        };
        var nodes = new Node[] { new() { Name = "node1", Address = "10.0.0.1" } };
        GivenConsulHealthReturnsEntries(entries);
        GivenConsulCatalogReturnsNodes(nodes);

        var expectedService = new Service("test-service", new ServiceHostAndPort("10.0.0.1", 8080), "id1", "v1", []);
        _serviceBuilder.Setup(x => x.BuildServices(entries, nodes)).Returns([expectedService]);

        // Act
        var result = await _sut.GetAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(expectedService, result[0]);
        _serviceBuilder.Verify(x => x.BuildServices(entries, nodes), Times.Once);
        _logger.Verify(x => x.LogDebug(It.IsAny<Func<string>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_WhenEntriesFound_LogsDebugTwice()
    {
        // Arrange
        var entries = new ServiceEntry[]
        {
            new() { Service = new AgentService { Service = "svc", Address = "localhost", Port = 80 } },
        };
        GivenConsulHealthReturnsEntries(entries);
        GivenConsulCatalogReturnsNodes(Array.Empty<Node>());
        _serviceBuilder.Setup(x => x.BuildServices(It.IsAny<ServiceEntry[]>(), It.IsAny<Node[]>())).Returns([]);

        // Act
        await _sut.GetAsync();

        // Assert
        _logger.Verify(x => x.LogDebug(It.IsAny<Func<string>>()), Times.Exactly(2));
        _logger.Verify(x => x.LogWarning(It.IsAny<Func<string>>()), Times.Never);
    }

    private void GivenConsulHealthReturnsEntries(ServiceEntry[] entries)
    {
        var result = new QueryResult<ServiceEntry[]> { Response = entries };
        _healthEndpoint
            .Setup(x => x.Service(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(result);
    }

    private void GivenConsulCatalogReturnsNodes(Node[] nodes)
    {
        var result = new QueryResult<Node[]> { Response = nodes };
        _catalogEndpoint
            .Setup(x => x.Nodes())
            .ReturnsAsync(result);
    }
}
