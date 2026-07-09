namespace Probe.DbEditor.Models;

public sealed class ConnectionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New connection";
    public string FolderId { get; set; } = "";
    public ConnectionFavoriteColor Color { get; set; }
    public ConnectionProtocolKind Protocol { get; set; } = ConnectionProtocolKind.Tcp;
    public string Host { get; set; } = "127.0.0.1";
    public uint Port { get; set; } = 3306;
    public string PipeName { get; set; } = "MYSQL";
    public string UserName { get; set; } = "root";
    public string Password { get; set; } = "";
    public bool SavePassword { get; set; }
    public string DefaultSchema { get; set; } = "";
    public DatabaseTlsMode TlsMode { get; set; } = DatabaseTlsMode.VerifyFull;
    public string SshHost { get; set; } = "";
    public uint SshPort { get; set; } = 22;
    public string SshUserName { get; set; } = "";
    public string SshPassword { get; set; } = "";
    public bool SaveSshPassword { get; set; }
    public string SshPrivateKeyPath { get; set; } = "";
    public string SshPrivateKeyPassphrase { get; set; } = "";

    public ConnectionProfile Clone(bool includeSecrets)
    {
        return new ConnectionProfile
        {
            Id = Id,
            Name = Name,
            FolderId = FolderId,
            Color = Color,
            Protocol = Protocol,
            Host = Host,
            Port = Port,
            PipeName = PipeName,
            UserName = UserName,
            Password = includeSecrets ? Password : "",
            SavePassword = SavePassword,
            DefaultSchema = DefaultSchema,
            TlsMode = TlsMode,
            SshHost = SshHost,
            SshPort = SshPort,
            SshUserName = SshUserName,
            SshPassword = includeSecrets ? SshPassword : "",
            SaveSshPassword = SaveSshPassword,
            SshPrivateKeyPath = SshPrivateKeyPath,
            SshPrivateKeyPassphrase = includeSecrets ? SshPrivateKeyPassphrase : ""
        };
    }
}
