namespace Ocelot.Discovery.Consul.UnitTests;

public class UnitTest : Unit
{
    public override CancellationToken CancelMe => TestContext.Current.CancellationToken;
}
