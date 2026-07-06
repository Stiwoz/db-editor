using MySqlConnector;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Services;

public static class DatabaseConnectionStringFactory
{
    public static string Create(ConnectionProfile profile, uint? tunnelPort)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            UserID = profile.UserName,
            Password = profile.Password,
            ApplicationName = "Probe.DbEditor",
            AllowUserVariables = true,
            PersistSecurityInfo = false,
            SslMode = MapTlsMode(profile.TlsMode)
        };

        if (!string.IsNullOrWhiteSpace(profile.DefaultSchema))
        {
            builder.Database = profile.DefaultSchema;
        }

        switch (profile.Protocol)
        {
            case ConnectionProtocolKind.NamedPipe:
                builder.Server = string.IsNullOrWhiteSpace(profile.Host) ? "." : profile.Host;
                builder.ConnectionProtocol = MySqlConnectionProtocol.Pipe;
                builder.PipeName = string.IsNullOrWhiteSpace(profile.PipeName) ? "MYSQL" : profile.PipeName;
                break;

            case ConnectionProtocolKind.SshTunnel:
                if (tunnelPort is null)
                {
                    throw new InvalidOperationException("SSH tunnel was not opened.");
                }

                builder.Server = "127.0.0.1";
                builder.Port = tunnelPort.Value;
                builder.ConnectionProtocol = MySqlConnectionProtocol.Socket;
                break;

            default:
                builder.Server = string.IsNullOrWhiteSpace(profile.Host) ? "127.0.0.1" : profile.Host;
                builder.Port = profile.Port == 0 ? 3306 : profile.Port;
                builder.ConnectionProtocol = MySqlConnectionProtocol.Socket;
                break;
        }

        return builder.ConnectionString;
    }

    private static MySqlSslMode MapTlsMode(DatabaseTlsMode tlsMode)
    {
        return tlsMode switch
        {
            DatabaseTlsMode.Disabled => MySqlSslMode.Disabled,
            DatabaseTlsMode.Required => MySqlSslMode.Required,
            DatabaseTlsMode.VerifyCA => MySqlSslMode.VerifyCA,
            DatabaseTlsMode.VerifyFull => MySqlSslMode.VerifyFull,
            _ => MySqlSslMode.Preferred
        };
    }
}
