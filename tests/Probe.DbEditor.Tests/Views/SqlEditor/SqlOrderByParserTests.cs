using System.ComponentModel;
using Probe.DbEditor.Views.SqlEditor;

namespace Probe.DbEditor.Tests.Views.SqlEditor;

[TestClass]
public sealed class SqlOrderByParserTests
{
    [TestMethod]
    public void Parse_ReturnsSimpleOrderByDirections()
    {
        const string sql = "SELECT id, name FROM users ORDER BY name DESC, id ASC LIMIT 10";

        var columns = SqlOrderByParser.Parse(sql);

        Assert.AreEqual(2, columns.Count);
        Assert.AreEqual("name", columns[0].ColumnName);
        Assert.AreEqual(ListSortDirection.Descending, columns[0].Direction);
        Assert.AreEqual("id", columns[1].ColumnName);
        Assert.AreEqual(ListSortDirection.Ascending, columns[1].Direction);
    }

    [TestMethod]
    public void Parse_DefaultsDirectionAndUsesLastQualifiedIdentifier()
    {
        const string sql = "SELECT created_at FROM users ORDER BY `users`.`created_at`";

        var columns = SqlOrderByParser.Parse(sql);

        Assert.AreEqual(1, columns.Count);
        Assert.AreEqual("created_at", columns[0].ColumnName);
        Assert.AreEqual(ListSortDirection.Ascending, columns[0].Direction);
    }

    [TestMethod]
    public void Parse_IgnoresNestedAndQuotedOrderByText()
    {
        const string sql = """
            SELECT 'ORDER BY ignored DESC', ROW_NUMBER() OVER (ORDER BY id) AS rn, `display name`
            FROM users
            ORDER BY `display name` DESC;
            """;

        var columns = SqlOrderByParser.Parse(sql);

        Assert.AreEqual(1, columns.Count);
        Assert.AreEqual("display name", columns[0].ColumnName);
        Assert.AreEqual(ListSortDirection.Descending, columns[0].Direction);
    }

    [TestMethod]
    public void Parse_SkipsOrderingExpressionsThatDoNotMapDirectlyToResultColumns()
    {
        const string sql = "SELECT id, name FROM users ORDER BY LOWER(name) DESC, 1, name IS NULL, id DESC";

        var columns = SqlOrderByParser.Parse(sql);

        Assert.AreEqual(1, columns.Count);
        Assert.AreEqual("id", columns[0].ColumnName);
        Assert.AreEqual(ListSortDirection.Descending, columns[0].Direction);
    }
}
