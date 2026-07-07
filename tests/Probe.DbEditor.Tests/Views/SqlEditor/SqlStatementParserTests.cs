using Probe.DbEditor.Views.SqlEditor;

namespace Probe.DbEditor.Tests.Views.SqlEditor;

[TestClass]
public sealed class SqlStatementParserTests
{
    [TestMethod]
    public void Parse_IgnoresSemicolonsInsideLiteralsAndComments()
    {
        const string sql = """
            SELECT ';';
            -- ignored ;
            UPDATE users SET name = 'a;b';
            /* ignored ; */
            DELETE FROM users WHERE name = "c;d";
            """;

        var statements = SqlStatementParser.Parse(sql);

        Assert.AreEqual(3, statements.Count);
        Assert.AreEqual(SqlStatementKind.Select, statements[0].Kind);
        Assert.AreEqual(SqlStatementKind.Update, statements[1].Kind);
        Assert.AreEqual(SqlStatementKind.Delete, statements[2].Kind);
        StringAssert.Contains(statements[1].Text, "'a;b'");
        StringAssert.Contains(statements[2].Text, "\"c;d\"");
    }

    [TestMethod]
    public void FindStatementAtOffset_ReturnsStatementContainingCaret()
    {
        const string sql = "SELECT 1;\nUPDATE users SET name = 'x';\nSELECT 2;";
        var caretOffset = sql.IndexOf("name", StringComparison.Ordinal);

        var statement = SqlStatementParser.FindStatementAtOffset(sql, caretOffset);

        Assert.IsNotNull(statement);
        Assert.AreEqual(SqlStatementKind.Update, statement.Kind);
        Assert.IsTrue(statement.Text.StartsWith("UPDATE users", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_TracksOffsetsForSelectedSql()
    {
        const int selectionStart = 42;
        const string selectedSql = "UPDATE users SET enabled = 1; DELETE FROM users WHERE enabled = 0;";

        var statements = SqlStatementParser.Parse(selectedSql, selectionStart);

        Assert.AreEqual(2, statements.Count);
        Assert.AreEqual(selectionStart, statements[0].StartOffset);
        Assert.AreEqual(selectionStart + "UPDATE users SET enabled = 1".Length, statements[0].EndOffset);
        Assert.AreEqual(selectionStart + selectedSql.IndexOf("DELETE", StringComparison.Ordinal), statements[1].StartOffset);
    }

    [TestMethod]
    public void Parse_IgnoresCommentOnlySegments()
    {
        const string sql = "-- comment ;\n/* block ; */";

        var statements = SqlStatementParser.Parse(sql);

        Assert.AreEqual(0, statements.Count);
    }
}
