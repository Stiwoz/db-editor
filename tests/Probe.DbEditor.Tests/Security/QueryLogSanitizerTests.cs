using MySqlConnector;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Tests.Security;

[TestClass]
public sealed class QueryLogSanitizerTests
{
    [TestMethod]
    public void SanitizeStatement_CollapsesWhitespaceAndRedactsSecrets()
    {
        const string statement = """
            SET password = 'super-secret',
                api_key = token-value,
                user_name = 'alice'
            """;

        var sanitized = QueryLogSanitizer.SanitizeStatement(statement);

        Assert.AreEqual("SET password = <redacted>, api_key = <redacted>, user_name = 'alice'", sanitized);
        Assert.IsFalse(sanitized.Contains("super-secret", StringComparison.Ordinal));
        Assert.IsFalse(sanitized.Contains("token-value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SanitizeStatement_TruncatesLongStatements()
    {
        var sanitized = QueryLogSanitizer.SanitizeStatement(new string('x', 4100));

        Assert.AreEqual(4003, sanitized.Length);
        Assert.IsTrue(sanitized.EndsWith("...", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SummarizeParameters_RedactsAllValues()
    {
        using var command = new MySqlCommand();
        command.Parameters.AddWithValue("@password", "plain-text");
        command.Parameters.AddWithValue("@id", 42);

        var summary = QueryLogSanitizer.SummarizeParameters(command);

        Assert.AreEqual("@password=<redacted>, @id=<redacted>", summary);
        Assert.IsFalse(summary.Contains("plain-text", StringComparison.Ordinal));
        Assert.IsFalse(summary.Contains("42", StringComparison.Ordinal));
    }
}
