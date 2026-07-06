using Renci.SshNet.Common;

namespace Probe.DbEditor.Security;

public static class HostKeyFingerprint
{
    public static bool Matches(HostKeyEventArgs hostKey, string expectedFingerprint)
    {
        return Matches(
            hostKey.FingerPrintSHA256,
            hostKey.FingerPrintMD5,
            expectedFingerprint);
    }

    internal static bool Matches(string sha256Fingerprint, string md5Fingerprint, string expectedFingerprint)
    {
        var normalizedExpected = Normalize(expectedFingerprint);
        if (string.IsNullOrWhiteSpace(normalizedExpected))
        {
            return true;
        }

        return normalizedExpected == Normalize(sha256Fingerprint)
            || normalizedExpected == Normalize(md5Fingerprint)
            || normalizedExpected == Normalize($"SHA256:{sha256Fingerprint}")
            || normalizedExpected == Normalize($"MD5:{md5Fingerprint}");
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
