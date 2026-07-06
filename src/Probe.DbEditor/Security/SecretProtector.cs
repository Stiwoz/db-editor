using System.Security.Cryptography;
using System.Text;

namespace Probe.DbEditor.Security;

public static class SecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Probe.DbEditor.ConnectionProfile.v1");

    public static string ProtectString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return "";
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        try
        {
            var protectedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public static string UnprotectString(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return "";
        }

        byte[]? protectedBytes = null;
        byte[]? plainBytes = null;
        try
        {
            protectedBytes = Convert.FromBase64String(protectedText);
            plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            return "";
        }
        catch (FormatException)
        {
            return "";
        }
        finally
        {
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
    }
}
