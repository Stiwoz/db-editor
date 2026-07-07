using System.Net.Sockets;
using MySqlConnector;
using Probe.DbEditor.Models;
using Renci.SshNet.Common;

namespace Probe.DbEditor.Services;

internal static class ConnectionFailureMessage
{
    public static string Create(Exception exception, ConnectionProtocolKind protocol)
    {
        var root = Unwrap(exception);
        return root switch
        {
            OperationCanceledException => TimeoutMessage(protocol),
            SshOperationTimeoutException => TimeoutMessage(protocol),
            SshAuthenticationException ex => $"SSH authentication failed: {ex.Message}",
            SshConnectionException ex => $"SSH tunnel failed: {ex.Message}",
            SocketException ex => $"Network connection failed: {ex.Message}",
            MySqlException ex => $"Database connection failed: {ex.Message}",
            InvalidOperationException ex => ex.Message,
            _ => $"Connection failed: {root.Message}"
        };
    }

    private static string TimeoutMessage(ConnectionProtocolKind protocol)
    {
        var target = protocol == ConnectionProtocolKind.SshTunnel
            ? "SSH tunnel or forwarded database endpoint"
            : "database endpoint";

        return $"Connection timed out after {ConnectionAttemptDefaults.TimeoutSeconds} seconds. Check the {target}.";
    }

    private static Exception Unwrap(Exception exception)
    {
        return exception is AggregateException { InnerExceptions.Count: 1 } aggregate
            ? Unwrap(aggregate.InnerExceptions[0])
            : exception;
    }
}
