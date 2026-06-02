using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Ocelot.Cache;
using Ocelot.Configuration;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;
using Ocelot.DependencyInjection;
using Ocelot.Logging;
using System.Text;

namespace Ocelot.Discovery.Consul;

/// <summary>
/// Consul Feature: <see cref="OcelotBuilderExtensions.AddConfigStoredInConsul(IOcelotBuilder)"/>.<br/>
/// Ocelot Features: <see cref="Features.AddOcelotConfigurationRepository(IServiceCollection)"/> and <see cref="IOcelotBuilder.AddConfigurationPoller()"/>.
/// </summary>
/// <remarks>
/// Feature Commit: <see href="https://github.com/ThreeMammals/Ocelot/commit/c3cd181b90fb5d5353b886073b3b7c66c12c6bab">c3cd181</see>.<br/>
/// Feature PR: <see href="https://github.com/ThreeMammals/Ocelot/pull/157/">157</see>.
/// </remarks>
public class ConsulFileConfigurationRepository : IFileConfigurationRepository
{
    private readonly IOcelotCache<FileConfiguration> _cache;
    private readonly string _configurationKey;
    private readonly IConsulClient _consul;
    private readonly IOcelotLogger _logger;

    public ConsulFileConfigurationRepository(
        IOptions<FileConfiguration> fileConfiguration,
        IOcelotCache<FileConfiguration> cache,
        IConsulClientFactory factory,
        IOcelotLoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ConsulFileConfigurationRepository>();
        _cache = cache;

        var provider = fileConfiguration.Value.GlobalConfiguration.ServiceDiscoveryProvider;
        _configurationKey = string.IsNullOrWhiteSpace(provider.ConfigurationKey)
            ? nameof(InternalConfiguration)
            : provider.ConfigurationKey;

        var config = new ConsulRegistryConfiguration(provider.Scheme, provider.Host,
            provider.Port, _configurationKey, provider.Token);
        _consul = factory.Get(config);
    }

    public async Task<FileConfiguration?> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = _cache.Get(_configurationKey, _configurationKey); // TODO Region is a key?
        if (config != null)
            return config;

        var queryResult = await _consul.KV.Get(_configurationKey, cancellationToken);
        if (queryResult.Response == null)
        {
            // TODO Add a warning or return empty config obj?
            return null;
        }

        var bytes = queryResult.Response.Value;
        var json = Encoding.UTF8.GetString(bytes);
        var consulConfig = JsonConvert.DeserializeObject<FileConfiguration>(json);
        return consulConfig;
    }

    public async Task SetAsync(FileConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);
        var bytes = Encoding.UTF8.GetBytes(json);
        var kvPair = new KVPair(_configurationKey)
        {
            Value = bytes,
        };

        var result = await _consul.KV.Put(kvPair, cancellationToken);
        if (result.Response)
        {
            _cache.AddOrUpdate(_configurationKey, configuration, _configurationKey, TimeSpan.FromSeconds(3)); // TODO Need TTL config option
            return;
        }

        // Unhappy path
        throw new ConsulConfigurationRepositoryException(
            $"Unable to set {nameof(FileConfiguration)} in {nameof(Consul)}, response status code from {nameof(Consul)} was {result.StatusCode}.");
    }

    public FileConfiguration? Get()
        => GetAsync(CancellationToken.None).GetAwaiter().GetResult();

    public void Set(FileConfiguration configuration)
        => SetAsync(configuration, CancellationToken.None).GetAwaiter();
}
