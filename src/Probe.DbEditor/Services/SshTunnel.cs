using Probe.DbEditor.Models;
using Probe.DbEditor.Security;
using Renci.SshNet;

namespace Probe.DbEditor.Services;

public sealed class SshTunnel : IDisposable
{
    private readonly SshClient _client;
    private readonly ForwardedPortLocal _forwardedPort;

    private SshTunnel(SshClient client, ForwardedPortLocal forwardedPort)
    {
        _client = client;
        _forwardedPort = forwardedPort;
    }

    public uint LocalPort => _forwardedPort.BoundPort;

    public static async Task<SshTunnel> OpenAsync(
        ConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profile.SshHost))
        {
            throw new InvalidOperationException("SSH host is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.SshUserName))
        {
            throw new InvalidOperationException("SSH username is required.");
        }

        var authenticationMethods = new List<AuthenticationMethod>();
        if (!string.IsNullOrWhiteSpace(profile.SshPrivateKeyPath))
        {
            var passphrase = string.IsNullOrEmpty(profile.SshPrivateKeyPassphrase)
                ? null
                : profile.SshPrivateKeyPassphrase;
            authenticationMethods.Add(new PrivateKeyAuthenticationMethod(
                profile.SshUserName,
                new PrivateKeyFile(profile.SshPrivateKeyPath, passphrase)));
        }

        if (!string.IsNullOrEmpty(profile.SshPassword))
        {
            authenticationMethods.Add(new PasswordAuthenticationMethod(profile.SshUserName, profile.SshPassword));
        }

        if (authenticationMethods.Count == 0)
        {
            throw new InvalidOperationException("SSH password or private key is required.");
        }

        var connectionInfo = new ConnectionInfo(
            profile.SshHost,
            (int)profile.SshPort,
            profile.SshUserName,
            authenticationMethods.ToArray());
        connectionInfo.Timeout = ConnectionAttemptDefaults.Timeout;

        var client = new SshClient(connectionInfo);
        if (!string.IsNullOrWhiteSpace(profile.SshHostKeyFingerprint))
        {
            client.HostKeyReceived += (_, args) =>
            {
                args.CanTrust = HostKeyFingerprint.Matches(args, profile.SshHostKeyFingerprint);
            };
        }

        try
        {
            await client.ConnectAsync(cancellationToken);

            var databaseHost = string.IsNullOrWhiteSpace(profile.SshDatabaseHost)
                ? "127.0.0.1"
                : profile.SshDatabaseHost;
            var databasePort = profile.SshDatabasePort == 0 ? 3306 : profile.SshDatabasePort;

            var forwardedPort = new ForwardedPortLocal("127.0.0.1", databaseHost, databasePort);
            client.AddForwardedPort(forwardedPort);
            forwardedPort.Start();

            return new SshTunnel(client, forwardedPort);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_forwardedPort.IsStarted)
        {
            _forwardedPort.Stop();
        }

        _client.Disconnect();
        _client.Dispose();
    }
}
