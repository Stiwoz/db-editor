using MySqlConnector;
using Probe.DbEditor.Models;
using Probe.DbEditor.Services;

namespace Probe.DbEditor.Tests.Services;

[TestClass]
public sealed class DatabaseConnectionStringFactoryTests
{
    [TestMethod]
    public void Create_BuildsTcpConnectionStringWithSecureDefaults()
    {
        var profile = new ConnectionProfile
        {
            Host = "",
            Port = 0,
            UserName = "app_user",
            Password = "secret",
            DefaultSchema = "app",
            TlsMode = DatabaseTlsMode.VerifyFull
        };

        var builder = new MySqlConnectionStringBuilder(DatabaseConnectionStringFactory.Create(profile, null));

        Assert.AreEqual("127.0.0.1", builder.Server);
        Assert.AreEqual(3306u, builder.Port);
        Assert.AreEqual("app_user", builder.UserID);
        Assert.AreEqual("secret", builder.Password);
        Assert.AreEqual("app", builder.Database);
        Assert.AreEqual(MySqlConnectionProtocol.Socket, builder.ConnectionProtocol);
        Assert.AreEqual(MySqlSslMode.VerifyFull, builder.SslMode);
        Assert.IsTrue(builder.AllowUserVariables);
        Assert.IsFalse(builder.PersistSecurityInfo);
    }

    [TestMethod]
    public void Create_BuildsNamedPipeConnectionString()
    {
        var profile = new ConnectionProfile
        {
            Protocol = ConnectionProtocolKind.NamedPipe,
            Host = "",
            PipeName = ""
        };

        var builder = new MySqlConnectionStringBuilder(DatabaseConnectionStringFactory.Create(profile, null));

        Assert.AreEqual(".", builder.Server);
        Assert.AreEqual("MYSQL", builder.PipeName);
        Assert.AreEqual(MySqlConnectionProtocol.Pipe, builder.ConnectionProtocol);
    }

    [TestMethod]
    public void Create_BuildsSshTunnelConnectionStringThroughLocalForward()
    {
        var profile = new ConnectionProfile
        {
            Protocol = ConnectionProtocolKind.SshTunnel,
            Host = "db.example.test",
            Port = 3307
        };

        var builder = new MySqlConnectionStringBuilder(DatabaseConnectionStringFactory.Create(profile, 4406));

        Assert.AreEqual("127.0.0.1", builder.Server);
        Assert.AreEqual(4406u, builder.Port);
        Assert.AreEqual(MySqlConnectionProtocol.Socket, builder.ConnectionProtocol);
    }

    [TestMethod]
    public void Create_RequiresOpenedSshTunnelPortForSshProfiles()
    {
        var profile = new ConnectionProfile { Protocol = ConnectionProtocolKind.SshTunnel };

        TestAssert.Throws<InvalidOperationException>(
            () => DatabaseConnectionStringFactory.Create(profile, null));
    }
}
