using Probe.DbEditor.Models;
using Probe.DbEditor.Services;

namespace Probe.DbEditor.Tests.Services;

[TestClass]
public sealed class SshTunnelTests
{
    [TestMethod]
    public void ResolveRemoteDatabaseEndpoint_UsesMainDatabaseHostAndPort()
    {
        var profile = new ConnectionProfile
        {
            Host = "db.internal.example",
            Port = 3307
        };

        var endpoint = SshTunnel.ResolveRemoteDatabaseEndpoint(profile);

        Assert.AreEqual("db.internal.example", endpoint.Host);
        Assert.AreEqual(3307u, endpoint.Port);
    }

    [TestMethod]
    public void ResolveRemoteDatabaseEndpoint_DefaultsEmptyMainDatabaseTarget()
    {
        var profile = new ConnectionProfile
        {
            Host = "",
            Port = 0
        };

        var endpoint = SshTunnel.ResolveRemoteDatabaseEndpoint(profile);

        Assert.AreEqual("127.0.0.1", endpoint.Host);
        Assert.AreEqual(3306u, endpoint.Port);
    }
}
