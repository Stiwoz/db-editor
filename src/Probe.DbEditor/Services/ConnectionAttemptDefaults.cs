namespace Probe.DbEditor.Services;

internal static class ConnectionAttemptDefaults
{
    public const uint TimeoutSeconds = 10;

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
}
