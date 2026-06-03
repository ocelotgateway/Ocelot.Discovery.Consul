using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Configuration;
using Ocelot.Configuration.Builder;
using Ocelot.Logging;
using Ocelot.ServiceDiscovery.Providers;

namespace Ocelot.Discovery.Consul.UnitTests;

public sealed class ConsulProviderFactoryTests : UnitTest, IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly DefaultHttpContext _context = new();

    public ConsulProviderFactoryTests()
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        _context.Items.Add(nameof(ConsulRegistryConfiguration), new ConsulRegistryConfiguration(null, null, 0, null, null));
        contextAccessor.SetupGet(x => x.HttpContext).Returns(_context);

        var loggerFactory = new Mock<IOcelotLoggerFactory>();
        var logger = new Mock<IOcelotLogger>();
        loggerFactory.Setup(x => x.CreateLogger<Consul>()).Returns(logger.Object);
        loggerFactory.Setup(x => x.CreateLogger<PollConsul>()).Returns(logger.Object);

        var consulFactory = new Mock<IConsulClientFactory>();
        var consulServiceBuilder = new Mock<IConsulServiceBuilder>();

        var services = new ServiceCollection();
        services.AddSingleton(contextAccessor.Object);
        services.AddSingleton(consulFactory.Object);
        services.AddSingleton(loggerFactory.Object);
        services.AddScoped(_ => consulServiceBuilder.Object);

        _provider = services.BuildServiceProvider(true); // validate scopes!!!
        _scope = _provider.CreateScope();
        _context.RequestServices = _scope.ServiceProvider;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    [Fact]
    public void Get_EmptyTypeName_ReturnedConsul()
    {
        // Arrange
        var emptyType = string.Empty;
        var route = GivenRoute(string.Empty);

        // Act
        var actual = CreateProvider(route, emptyType);

        // Assert
        Assert.NotNull(actual);
        Assert.IsType<Consul>(actual);
    }

    [Fact]
    public void Get_PollConsulTypeName_ReturnedPollConsul()
    {
        // Arrange, Act
        var route = GivenRoute(string.Empty);
        var actual = CreateProvider(route, nameof(PollConsul));

        // Assert
        Assert.NotNull(actual);
        Assert.IsType<PollConsul>(actual);
    }

    [Fact]
    public void Get_RoutesWithTheSameServiceName_ReturnedSameProvider()
    {
        // Arrange, Act: 1
        var route1 = GivenRoute("test");
        var actual1 = CreateProvider(route1);

        // Arrange, Act: 2
        var route2 = GivenRoute("test");
        var actual2 = CreateProvider(route2);

        // Assert
        Assert.NotNull(actual1);
        var provider1 = Assert.IsType<PollConsul>(actual1);

        Assert.NotNull(actual2);
        var provider2 = Assert.IsType<PollConsul>(actual2);

        Assert.Same(actual1, actual2);
        Assert.Equal(provider1.ServiceName, provider2.ServiceName);
    }

    [Fact]
    public void Get_MultipleServiceNames_ShouldReturnProviderAccordingToServiceName()
    {
        string[] serviceNames = ["service1", "service2", "service3", "service4"];
        var providersList = serviceNames.Select(DummyPollingConsulServiceFactory).ToList();

        foreach (var serviceName in serviceNames)
        {
            var currentProvider = DummyPollingConsulServiceFactory(serviceName);
            Assert.Contains(currentProvider, providersList);
        }

        var convertedProvidersList = providersList.Select(x => x as PollConsul).ToList();
        convertedProvidersList.ForEach(Assert.NotNull);

        foreach (var serviceName in serviceNames)
        {
            var cProvider = DummyPollingConsulServiceFactory(serviceName);
            var convertedCProvider = cProvider as PollConsul;
            Assert.NotNull(convertedCProvider);

            var matchingProviders = convertedProvidersList
                .Where(x => x?.ServiceName == convertedCProvider.ServiceName)
                .ToList();
            var first = Assert.Single(matchingProviders);
            Assert.NotNull(first);
            Assert.Equal(convertedCProvider.ServiceName, first.ServiceName);
        }
    }

    [Fact]
    [Trait("Bug", "2178")] // https://github.com/ThreeMammals/Ocelot/issues/2178
    public void Get_RootProvider_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var route = GivenRoute(string.Empty);
        _context.RequestServices = _provider; // given service provider is root provider

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => CreateProvider(route));
    }

    private IServiceDiscoveryProvider DummyPollingConsulServiceFactory(string serviceName) => CreateProvider(GivenRoute(serviceName));

    private static DownstreamRoute GivenRoute(string serviceName) => new DownstreamRouteBuilder()
        .WithServiceName(serviceName)
        .Build();

    private IServiceDiscoveryProvider CreateProvider(DownstreamRoute route, string providerType = ConsulProviderFactory.PollConsul)
    {
        var stopsFromPolling = 10000;
        return ConsulProviderFactory.Get.Invoke(
            _provider,
            new ServiceProviderConfiguration()
            {
                Type = providerType,
                Scheme = Uri.UriSchemeHttp,
                PollingInterval = stopsFromPolling,
            }, 
            route);
    }
}
