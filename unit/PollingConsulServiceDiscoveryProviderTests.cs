using Ocelot.Logging;
using Ocelot.ServiceDiscovery.Providers;
using Ocelot.Values;

namespace Ocelot.Discovery.Consul.UnitTests;

public class PollingConsulServiceDiscoveryProviderTests : UnitTest
{
    private readonly int _delay;
    private readonly List<Service> _services;
    private readonly Mock<IOcelotLoggerFactory> _factory;
    private readonly Mock<IOcelotLogger> _logger;
    private readonly Mock<IServiceDiscoveryProvider> _consulServiceDiscoveryProvider;
    private List<Service> _result;

    public PollingConsulServiceDiscoveryProviderTests()
    {
        _services = [];
        _delay = 1;
        _factory = new Mock<IOcelotLoggerFactory>();
        _logger = new Mock<IOcelotLogger>();
        _factory.Setup(x => x.CreateLogger<PollConsul>()).Returns(_logger.Object);
        _consulServiceDiscoveryProvider = new Mock<IServiceDiscoveryProvider>();
    }

    [Fact]
    public async Task Should_return_service_from_consul()
    {
        // Arrange
        var service = new Service(string.Empty, new ServiceHostAndPort(string.Empty, 0), string.Empty, string.Empty, []);
        GivenConsulReturns(service);

        // Act
        await WhenIGetTheServices(1);

        // Assert
        Assert.Single(_result);
    }

    [Fact]
    public async Task Should_return_service_from_consul_without_delay()
    {
        // Arrange
        var service = new Service(string.Empty, new ServiceHostAndPort(string.Empty, 0), string.Empty, string.Empty, []);
        GivenConsulReturns(service);

        // Act
        await WhenIGetTheServicesWithoutDelay(1);

        // Assert
        Assert.Single(_result);
    }

    private void GivenConsulReturns(Service service)
    {
        _services.Add(service);
        _consulServiceDiscoveryProvider.Setup(x => x.GetAsync()).ReturnsAsync(_services);
    }

    private async Task WhenIGetTheServices(int expected)
    {
        var provider = new PollConsul(_delay, "test", _factory.Object, _consulServiceDiscoveryProvider.Object);
        var result = await Wait.For(3_000).UntilAsync(async (ct) =>
        {
            try
            {
                _result = await provider.GetAsync();
                return _result.Count == expected;
            }
            catch (Exception)
            {
                return false;
            }
        }, CancelMe);
        Assert.True(result);
    }

    private async Task WhenIGetTheServicesWithoutDelay(int expected)
    {
        var provider = new PollConsul(_delay, "test2", _factory.Object, _consulServiceDiscoveryProvider.Object);
        bool result;
        try
        {
            _result = await provider.GetAsync();
            result = _result.Count == expected;
        }
        catch (Exception)
        {
            result = false;
        }

        Assert.True(result);
    }

    [Fact]
    public async Task GetAsync_WhenServicesAvailableAndPollingIntervalNotExpired_ReturnsCachedServices()
    {
        // Arrange: large polling interval so cache never expires during test
        const int largePollingInterval = 600_000; // 10 minutes
        var service = new Service(string.Empty, new ServiceHostAndPort(string.Empty, 0), string.Empty, string.Empty, []);
        GivenConsulReturns(service);
        var provider = new PollConsul(largePollingInterval, "cached-service", _factory.Object, _consulServiceDiscoveryProvider.Object);

        // Act: first call populates the cache
        var firstResult = await provider.GetAsync();

        // Act: second call should use cached result
        _result = await provider.GetAsync();

        // Assert: consul was only queried once
        Assert.Single(_result);
        _consulServiceDiscoveryProvider.Verify(x => x.GetAsync(), Times.Once);
    }
}
