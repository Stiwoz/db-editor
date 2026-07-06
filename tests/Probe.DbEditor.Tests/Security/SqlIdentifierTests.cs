using Probe.DbEditor.Security;

namespace Probe.DbEditor.Tests.Security;

[TestClass]
public sealed class SqlIdentifierTests
{
    [TestMethod]
    public void Quote_WrapsIdentifierAndEscapesBackticks()
    {
        Assert.AreEqual("`user``events`", SqlIdentifier.Quote("user`events"));
        Assert.AreEqual("`app`.`user``events`", SqlIdentifier.QuoteQualified("app", "user`events"));
    }

    [TestMethod]
    public void Quote_RejectsUnsafeDatabaseIdentifiers()
    {
        TestAssert.Throws<InvalidOperationException>(() => SqlIdentifier.Quote(""));
        TestAssert.Throws<InvalidOperationException>(() => SqlIdentifier.Quote(new string('a', 65)));
        TestAssert.Throws<InvalidOperationException>(() => SqlIdentifier.Quote("valid\0invalid"));
    }

    [TestMethod]
    [DataRow("idx_name", "idx_name")]
    [DataRow(" _idx$1 ", "_idx$1")]
    public void ValidateUserDefinedIdentifier_ReturnsTrimmedSafeIdentifier(string input, string expected)
    {
        Assert.AreEqual(expected, SqlIdentifier.ValidateUserDefinedIdentifier(input, "Index name"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("1index")]
    [DataRow("index-name")]
    [DataRow("index name")]
    [DataRow("idx;DROP_TABLE")]
    public void ValidateUserDefinedIdentifier_RejectsInvalidUserInput(string input)
    {
        TestAssert.Throws<InvalidOperationException>(
            () => SqlIdentifier.ValidateUserDefinedIdentifier(input, "Index name"));
    }

    [TestMethod]
    public void ValidateColumnAllowList_AllowsOnlyKnownColumnsCaseInsensitively()
    {
        var result = SqlIdentifier.ValidateColumnAllowList(
            [" id ", "EMAIL"],
            ["id", "email", "created_at"]);

        CollectionAssert.AreEqual(new[] { "id", "EMAIL" }, result.ToArray());
    }

    [TestMethod]
    public void ValidateColumnAllowList_RejectsUnknownOrEmptyLists()
    {
        TestAssert.Throws<InvalidOperationException>(
            () => SqlIdentifier.ValidateColumnAllowList(["email; DROP TABLE users"], ["email"]));

        TestAssert.Throws<InvalidOperationException>(
            () => SqlIdentifier.ValidateColumnAllowList([], ["email"]));
    }
}
