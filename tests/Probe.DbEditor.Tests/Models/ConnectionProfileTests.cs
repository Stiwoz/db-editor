using Probe.DbEditor.Models;

namespace Probe.DbEditor.Tests.Models;

[TestClass]
public sealed class ConnectionProfileTests
{
    [TestMethod]
    public void Clone_ExcludesSecretsWhenRequested()
    {
        var profile = FullProfile();

        var clone = profile.Clone(includeSecrets: false);

        Assert.AreEqual(profile.Id, clone.Id);
        Assert.AreEqual(profile.Name, clone.Name);
        Assert.AreEqual(profile.Protocol, clone.Protocol);
        Assert.AreEqual(profile.Host, clone.Host);
        Assert.AreEqual(profile.SshHost, clone.SshHost);
        Assert.AreEqual("", clone.Password);
        Assert.AreEqual("", clone.SshPassword);
        Assert.AreEqual("", clone.SshPrivateKeyPassphrase);
    }

    [TestMethod]
    public void Clone_IncludesSecretsWhenRequested()
    {
        var profile = FullProfile();

        var clone = profile.Clone(includeSecrets: true);

        Assert.AreEqual("database-secret", clone.Password);
        Assert.AreEqual("ssh-secret", clone.SshPassword);
        Assert.AreEqual("key-passphrase", clone.SshPrivateKeyPassphrase);
    }

    private static ConnectionProfile FullProfile()
    {
        return new ConnectionProfile
        {
            Id = "profile-id",
            Name = "Production",
            Protocol = ConnectionProtocolKind.SshTunnel,
            Host = "db.example.test",
            Port = 3307,
            PipeName = "custom-pipe",
            UserName = "app_user",
            Password = "database-secret",
            SavePassword = true,
            DefaultSchema = "app",
            TlsMode = DatabaseTlsMode.Required,
            SshHost = "ssh.example.test",
            SshPort = 2222,
            SshUserName = "ssh_user",
            SshPassword = "ssh-secret",
            SaveSshPassword = true,
            SshPrivateKeyPath = "C:\\keys\\id_ed25519",
            SshPrivateKeyPassphrase = "key-passphrase",
            SshHostKeyFingerprint = "SHA256:abcdef",
            SshDatabaseHost = "127.0.0.2",
            SshDatabasePort = 3308
        };
    }
}
