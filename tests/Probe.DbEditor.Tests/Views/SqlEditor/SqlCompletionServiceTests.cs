using ICSharpCode.AvalonEdit.Document;
using Probe.DbEditor.Views.SqlEditor;

namespace Probe.DbEditor.Tests.Views.SqlEditor;

[TestClass]
public sealed class SqlCompletionServiceTests
{
    [TestMethod]
    public void BuildCompletionData_ReturnsKeywordsFunctionsAndVariablesByPrefix()
    {
        var service = new SqlCompletionService();

        var completions = service.BuildCompletionData("CO", "SELECT @customer_id, :limit").Select(item => item.Text).ToArray();

        CollectionAssert.Contains(completions, "COALESCE()");
        Assert.IsFalse(completions.Contains("SELECT"));

        var variableCompletions = service.BuildCompletionData("@c", "SELECT @customer_id, :limit").Select(item => item.Text).ToArray();
        CollectionAssert.Contains(variableCompletions, "@customer_id");
    }

    [TestMethod]
    public void BuildCompletionData_QuotesIdentifiersThatNeedQuotingAndPreservesUnderscores()
    {
        var service = new SqlCompletionService();
        service.AddSchema("tenant-data");
        service.AddTable("tenant-data", "save_missing_files");
        service.AddTable("tenant-data", "order items");
        service.AddColumn("save_missing_files", "toolkitendpoint_table_name");

        var completions = service.BuildCompletionData("toolkit", "").Select(item => item.Text).ToArray();
        CollectionAssert.Contains(completions, "toolkitendpoint_table_name");

        var schemaCompletions = service.BuildCompletionData("`tenant", "").Select(item => item.Text).ToArray();
        CollectionAssert.Contains(schemaCompletions, "`tenant-data`");

        var tableCompletions = service.BuildCompletionData("save", "").Select(item => item.Text).ToArray();
        CollectionAssert.Contains(tableCompletions, "`tenant-data`.save_missing_files");

        var quotedTableCompletions = service.BuildCompletionData("`order", "").Select(item => item.Text).ToArray();
        CollectionAssert.Contains(quotedTableCompletions, "`tenant-data`.`order items`");
    }

    [TestMethod]
    public void GetPrefix_UsesSqlIdentifierCharactersAroundCaret()
    {
        var document = new TextDocument("SELECT save_missing_files.toolkitendpoint_table_name FROM x");
        var caretOffset = "SELECT save_missing_files.toolkit".Length;

        Assert.AreEqual("save_missing_files.toolkit", SqlCompletionService.GetPrefix(document, caretOffset));
        Assert.IsTrue(SqlCompletionService.IsCompletionCharacter('_'));
        Assert.IsTrue(SqlCompletionService.IsCompletionCharacter('.'));
        Assert.IsTrue(SqlCompletionService.IsCompletionCharacter('`'));
        Assert.IsFalse(SqlCompletionService.IsCompletionCharacter(';'));
    }
}
