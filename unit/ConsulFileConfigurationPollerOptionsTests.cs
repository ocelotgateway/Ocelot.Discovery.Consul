using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;

namespace Ocelot.Discovery.Consul.UnitTests;

public class ConsulFileConfigurationPollerOptionsTests : UnitTest
{
    private readonly Mock<IInternalConfigurationRepository> _internalRepo = new();
    private readonly Mock<IFileConfigurationRepository> _fileRepo = new();

    [Fact]
    public void Ctor_ValidDependencies_CreatesInstance()
    {
        // Act
        var sut = new ConsulFileConfigurationPollerOptions(_internalRepo.Object, _fileRepo.Object);

        // Assert
        Assert.NotNull(sut);
    }

    [Fact]
    public void GetDelay_WhenFileConfigHasPollingInterval_ReturnsInterval()
    {
        // Arrange
        const int expectedDelay = 7000;
        var fileConfig = new FileConfiguration
        {
            GlobalConfiguration = new FileGlobalConfiguration
            {
                ServiceDiscoveryProvider = new FileServiceDiscoveryProvider
                {
                    PollingInterval = expectedDelay,
                },
            },
        };
        _fileRepo.Setup(x => x.Get()).Returns(fileConfig);

        var sut = new ConsulFileConfigurationPollerOptions(_internalRepo.Object, _fileRepo.Object);

        // Act
        var delay = sut.Delay();

        // Assert
        Assert.Equal(expectedDelay, delay);
    }

    [Fact]
    public void GetDelay_WhenFileConfigIsNull_ReturnsDefaultDelay()
    {
        // Arrange
        _fileRepo.Setup(x => x.Get()).Returns((FileConfiguration)null);
        _internalRepo.Setup(x => x.Get()).Returns((Ocelot.Configuration.IInternalConfiguration)null);

        var sut = new ConsulFileConfigurationPollerOptions(_internalRepo.Object, _fileRepo.Object);

        // Act
        var delay = sut.Delay();

        // Assert
        Assert.Equal(1000, delay);
    }

    [Fact]
    public void GetDelay_CallsBaseImplementation_DelegatingToFileRepo()
    {
        // Arrange
        _fileRepo.Setup(x => x.Get()).Returns((FileConfiguration)null);
        _internalRepo.Setup(x => x.Get()).Returns((Ocelot.Configuration.IInternalConfiguration)null);

        var sut = new ConsulFileConfigurationPollerOptions(_internalRepo.Object, _fileRepo.Object);

        // Act
        sut.Delay();

        // Assert: base implementation reads from file repo
        _fileRepo.Verify(x => x.Get(), Times.Once);
    }
}
