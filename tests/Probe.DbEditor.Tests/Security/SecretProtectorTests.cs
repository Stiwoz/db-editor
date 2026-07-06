using Probe.DbEditor.Security;

namespace Probe.DbEditor.Tests.Security;

[TestClass]
public sealed class SecretProtectorTests
{
    [TestMethod]
    public void ProtectString_RoundTripsWithoutReturningPlainTextCipherText()
    {
        const string secret = "correct horse battery staple";

        var protectedText = SecretProtector.ProtectString(secret);
        var unprotectedText = SecretProtector.UnprotectString(protectedText);

        Assert.AreNotEqual(secret, protectedText);
        Assert.AreEqual(secret, unprotectedText);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-base64")]
    public void UnprotectString_ReturnsEmptyForMissingOrInvalidProtectedText(string protectedText)
    {
        Assert.AreEqual("", SecretProtector.UnprotectString(protectedText));
    }
}
