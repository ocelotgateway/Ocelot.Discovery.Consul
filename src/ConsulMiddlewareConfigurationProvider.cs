using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ocelot.Configuration;
using Ocelot.Configuration.Creator;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;
using Ocelot.Errors;
using Ocelot.Infrastructure.Extensions;
using Ocelot.Middleware;
using Ocelot.Responses;

namespace Ocelot.Discovery.Consul;

public static class ConsulMiddlewareConfigurationProvider
{
    public static OcelotMiddlewareConfigurationDelegate Get { get; } = GetAsync;

    private static async Task GetAsync(IApplicationBuilder builder)
    {
        var fileConfigRepo = builder.ApplicationServices.GetService<IFileConfigurationRepository>();
        var fileConfig = builder.ApplicationServices.GetService<IOptionsMonitor<FileConfiguration>>();
        var internalConfigCreator = builder.ApplicationServices.GetService<IInternalConfigurationCreator>();
        var internalConfigRepo = builder.ApplicationServices.GetService<IInternalConfigurationRepository>();

        if (UsingConsul(fileConfigRepo))
        {
            await SetFileConfigInConsul(builder, fileConfigRepo, fileConfig, internalConfigCreator, internalConfigRepo);
        }
    }

    private static bool UsingConsul(IFileConfigurationRepository repo)
        => repo.GetType() == typeof(ConsulFileConfigurationRepository);

    private static async Task SetFileConfigInConsul(IApplicationBuilder builder,
        IFileConfigurationRepository repository, IOptionsMonitor<FileConfiguration> options,
        IInternalConfigurationCreator creator, IInternalConfigurationRepository internalRepo)
    {
        // Get the config from Consul
        var configuration = await repository.GetAsync(CancellationToken.None); // TODO Inject real token
        if (configuration is null)
        {
            // there was no config in Consul set the file in config in Consul
            await repository.SetAsync(options.CurrentValue);
            return;
        }

        // Create the internal config from Consul data
        var config = await creator.Create(configuration)
            ?? new ErrorResponse<IInternalConfiguration>(new UnknownError($"The {creator.GetType().Name} service returned nothing"));
        if (config.IsError)
            throw new Exception($"Unable to start Ocelot, errors are:{config.Errors.ToErrorString(true, true)}");

        // Add the internal config to the internal repo
        _ = internalRepo.AddOrReplace(config.Data);
    }
}
