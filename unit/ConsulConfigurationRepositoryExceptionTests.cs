using Ocelot.Configuration.Repository;
using Ocelot.Errors;

namespace Ocelot.Discovery.Consul.UnitTests;

public class ConsulConfigurationRepositoryExceptionTests : UnitTest
{
    [Fact]
    public void Ctor_StringMessage_SetsMessageProperty()
    {
        // Arrange
        const string message = "Something went wrong in Consul";

        // Act
        var sut = new ConsulConfigurationRepositoryException(message);

        // Assert
        Assert.Equal(message, sut.Message);
    }

    [Fact]
    public void Ctor_StringMessage_IsExceptionSubclass()
    {
        // Act
        var sut = new ConsulConfigurationRepositoryException("error");

        // Assert
        Assert.IsAssignableFrom<ConfigurationRepositoryException>(sut);
        Assert.Equal("error", sut.Message);
    }

    [Fact]
    public void Ctor_Errors_SetsErrorsProperty()
    {
        // Arrange
        var errors = new List<Error> { new UnknownError("consul error") };

        // Act
        var sut = new ConsulConfigurationRepositoryException(errors);

        // Assert
        Assert.NotNull(sut);
        Assert.IsAssignableFrom<ConfigurationRepositoryException>(sut);
        Assert.Equal("UnknownError: consul error", sut.Message);
    }

    [Fact]
    public void Ctor_EmptyErrors_CreatesInstance()
    {
        // Arrange
        var errors = new List<Error>();

        // Act
        var sut = new ConsulConfigurationRepositoryException(errors);

        // Assert
        Assert.NotNull(sut);
    }
}
