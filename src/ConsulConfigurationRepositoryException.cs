using Ocelot.Configuration.Repository;
using Ocelot.Errors;

namespace Ocelot.Discovery.Consul;

public class ConsulConfigurationRepositoryException : ConfigurationRepositoryException
{
    public ConsulConfigurationRepositoryException(string message)
        : base(message)
    { }

    public ConsulConfigurationRepositoryException(IEnumerable<Error> errors)
        : base(errors)
    { }
}
