using Probe.DbEditor.Security;

namespace Probe.DbEditor.Tests.Security;

[TestClass]
public sealed class HostKeyFingerprintTests
{
    [TestMethod]
    [DataRow("SHA256:ab cd:ef")]
    [DataRow("abcdef")]
    [DataRow("MD5:11:22:aa")]
    [DataRow("1122AA")]
    public void Matches_NormalizesCommonFingerprintFormats(string expected)
    {
        Assert.IsTrue(HostKeyFingerprint.Matches("AB:CD:EF", "11:22:aa", expected));
    }

    [TestMethod]
    public void Matches_AllowsEmptyExpectedFingerprintAndRejectsMismatches()
    {
        Assert.IsTrue(HostKeyFingerprint.Matches("sha", "md5", ""));
        Assert.IsFalse(HostKeyFingerprint.Matches("sha", "md5", "other"));
    }
}
