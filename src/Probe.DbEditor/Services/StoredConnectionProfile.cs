using Probe.DbEditor.Models;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Services;

internal sealed class StoredConnectionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New connection";
    public ConnectionProtocolKind Protocol { get; set; } = ConnectionProtocolKind.Tcp;
    public string Host { get; set; } = "127.0.0.1";
    public uint Port { get; set; } = 3306;
    public string PipeName { get; set; } = "MYSQL";
    public string UserName { get; set; } = "root";
    public bool SavePassword { get; set; }
    public string ProtectedPassword { get; set; } = "";
    public string DefaultSchema { get; set; } = "";
    public DatabaseTlsMode TlsMode { get; set; } = DatabaseTlsMode.VerifyFull;
    public string SshHost { get; set; } = "";
    public uint SshPort { get; set; } = 22;
    public string SshUserName { get; set; } = "";
    public bool SaveSshPassword { get; set; }
    public string ProtectedSshPassword { get; set; } = "";
    public string SshPrivateKeyPath { get; set; } = "";
    public string SshHostKeyFingerprint { get; set; } = "";
    public string SshDatabaseHost { get; set; } = "127.0.0.1";
    public uint SshDatabasePort { get; set; } = 3306;

    public static StoredConnectionProfile FromProfile(ConnectionProfile profile)
    {
        return new StoredConnectionProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            Protocol = profile.Protocol,
            Host = profile.Host,
            Port = profile.Port,
            PipeName = profile.PipeName,
            UserName = profile.UserName,
            SavePassword = profile.SavePassword,
            ProtectedPassword = profile.SavePassword ? SecretProtector.ProtectString(profile.Password) : "",
            DefaultSchema = profile.DefaultSchema,
            TlsMode = profile.TlsMode,
            SshHost = profile.SshHost,
            SshPort = profile.SshPort,
            SshUserName = profile.SshUserName,
            SaveSshPassword = profile.SaveSshPassword,
            ProtectedSshPassword = profile.SaveSshPassword ? SecretProtector.ProtectString(profile.SshPassword) : "",
            SshPrivateKeyPath = profile.SshPrivateKeyPath,
            SshHostKeyFingerprint = profile.SshHostKeyFingerprint,
            SshDatabaseHost = profile.SshDatabaseHost,
            SshDatabasePort = profile.SshDatabasePort
        };
    }

    public ConnectionProfile ToProfile()
    {
        var password = SavePassword ? SecretProtector.UnprotectString(ProtectedPassword) : "";
        var sshPassword = SaveSshPassword ? SecretProtector.UnprotectString(ProtectedSshPassword) : "";
        return new ConnectionProfile
        {
            Id = Id,
            Name = Name,
            Protocol = Protocol,
            Host = Host,
            Port = Port,
            PipeName = PipeName,
            UserName = UserName,
            Password = password,
            SavePassword = SavePassword && !string.IsNullOrEmpty(password),
            DefaultSchema = DefaultSchema,
            TlsMode = TlsMode,
            SshHost = SshHost,
            SshPort = SshPort,
            SshUserName = SshUserName,
            SshPassword = sshPassword,
            SaveSshPassword = SaveSshPassword && !string.IsNullOrEmpty(sshPassword),
            SshPrivateKeyPath = SshPrivateKeyPath,
            SshHostKeyFingerprint = SshHostKeyFingerprint,
            SshDatabaseHost = SshDatabaseHost,
            SshDatabasePort = SshDatabasePort
        };
    }
}
