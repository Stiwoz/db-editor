using Renci.SshNet.Common;

namespace Probe.DbEditor.Security;

public static class HostKeyFingerprint
{
    public static bool Matches(HostKeyEventArgs hostKey, string expectedFingerprint)
    {
        var normalizedExpected = Normalize(expectedFingerprint);
        if (string.IsNullOrWhiteSpace(normalizedExpected))
        {
            return true;
        }

        return normalizedExpected == Normalize(hostKey.FingerPrintSHA256)
            || normalizedExpected == Normalize(hostKey.FingerPrintMD5)
            || normalizedExpected == Normalize($"SHA256:{hostKey.FingerPrintSHA256}")
            || normalizedExpected == Normalize($"MD5:{hostKey.FingerPrintMD5}");
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .Replace("SHA256:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("MD5:", "", StringComparison.OrdinalIgnoreCase)
            .Replace(":", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}
