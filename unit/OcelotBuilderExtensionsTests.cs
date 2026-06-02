using Consul;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.DependencyInjection;
using Ocelot.Values;
using System.Reflection;

namespace Ocelot.Discovery.Consul.UnitTests;

public class OcelotBuilderExtensionsTests : UnitTest
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configRoot;

    public OcelotBuilderExtensionsTests()
    {
        _configRoot = new ConfigurationRoot([]);
        _services = new ServiceCollection();
        _services.AddSingleton(GetHostingEnvironment());
        _services.AddSingleton(_configRoot);
    }

    private static IWebHostEnvironment GetHostingEnvironment()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.ApplicationName)
            .Returns(typeof(OcelotBuilderExtensionsTests).GetTypeInfo().Assembly.GetName().Name ?? string.Empty);
        return environment.Object;
    }

    [Fact]
    public void AddConsul_ShouldSetUpConsul()
    {
        // Arrange, Act
        var builder = _services.AddOcelot(_configRoot)
            .AddConsul();

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void AddConfigStoredInConsul_ShouldSetUpConsul()
    {
        // Arrange, Act
        var builder = _services.AddOcelot(_configRoot)
            .AddConsul()
            .AddConfigStoredInConsul();

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void AddConsulGeneric_TServiceBuilder_ShouldSetUpConsul()
    {
        // Arrange, Act
        var builder = _services
            .AddOcelot(_configRoot)
            .AddConsul<FakeConsulServiceBuilder>();

        // Assert
        Assert.NotNull(builder);
        var service = Assert.Single(builder.Services, s => s.ServiceType == typeof(IConsulServiceBuilder));
        Assert.NotNull(service);
    }
}

internal class FakeConsulServiceBuilder : IConsulServiceBuilder
{
    public ConsulRegistryConfiguration Configuration => throw new NotImplementedException();
    public IEnumerable<Service> BuildServices(ServiceEntry[] entries, Node[] nodes) => throw new NotImplementedException();
    public Service CreateService(ServiceEntry serviceEntry, Node serviceNode) => throw new NotImplementedException();
    public bool IsValid(ServiceEntry entry) => throw new NotImplementedException();
}
