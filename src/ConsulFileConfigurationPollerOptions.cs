using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;

namespace Ocelot.Discovery.Consul;

public class ConsulFileConfigurationPollerOptions(
    IInternalConfigurationRepository internalRepo,
    IFileConfigurationRepository fileRepo)
    : ServiceDiscoveryFileConfigurationPollerOptions(internalRepo, fileRepo), IFileConfigurationPollerOptions
{
    protected override int GetDelay(FileConfiguration configuration)
    {
        // The argument might be read from anywhere, but it should be handled by IFileConfigurationRepository service.
        // The user can define custom Metadata for the option: static, varying, or even a function based on statistics or a schedule.
        // For now, we utilize base Ocelot's functionality -> configuration.GlobalConfiguration.ServiceDiscoveryProvider.PollingInterval
        return base.GetDelay(configuration);
    }
}
