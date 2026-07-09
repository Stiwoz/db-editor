namespace Probe.DbEditor.Tests.Interaction;

[TestClass]
public sealed class SessionViewWorkspaceTests
{
    [TestMethod]
    public async Task SessionView_TableTabs_AreHiddenUntilTableSelectionAndContentIsSelected()
    {
        var xaml = await ReadFixtureAsync("SessionView.xaml");
        var source = await ReadFixtureAsync("SessionView.xaml.cs");

        StringAssert.Contains(xaml, "x:Name=\"ContentTab\" Header=\"Content\" Visibility=\"Collapsed\"");
        StringAssert.Contains(xaml, "x:Name=\"IndexesTab\" Header=\"Indexes\" Visibility=\"Collapsed\"");
        StringAssert.Contains(xaml, "x:Name=\"OverviewTab\" Header=\"Overview\" Visibility=\"Collapsed\"");
        StringAssert.Contains(xaml, "x:Name=\"QueryTab\" Header=\"Query\" IsSelected=\"True\"");
        StringAssert.Contains(source, "SetTableTabsVisible(false);");
        StringAssert.Contains(source, "SetTableTabsVisible(true);");
        StringAssert.Contains(source, "WorkspaceTabs.SelectedItem = ContentTab;");
        StringAssert.Contains(source, "private void ClearSelectedTableWorkspace()");
    }

    [TestMethod]
    public async Task SessionView_QueryResultGrid_IsHiddenUntilQueryCompletes()
    {
        var xaml = await ReadFixtureAsync("SessionView.xaml");
        var source = await ReadFixtureAsync("SessionView.xaml.cs");
        var showQueryResults = ExtractMethod(source, "private void ShowQueryResults");

        StringAssert.Contains(xaml, "x:Name=\"QueryResultGrid\"");
        StringAssert.Contains(xaml, "AutoGenerateColumns=\"False\"");
        StringAssert.Contains(xaml, "Visibility=\"Collapsed\"");
        StringAssert.Contains(source, "HideQueryResults();");
        StringAssert.Contains(source, "ShowQueryResults(summary.LastRows, summary.LastRowsOrdering);");
        AssertInOrder(
            showQueryResults,
            "QueryResultGrid.ItemsSource = null;",
            "QueryResultGrid.Columns.Clear();",
            "CreateQueryResultColumns(rows, ordering);",
            "QueryResultGrid.Visibility = Visibility.Visible;",
            "QueryResultGrid.ItemsSource = rows.DefaultView;",
            "QueryResultGrid.Items.Refresh();");
        StringAssert.Contains(source, "private void CreateQueryResultColumns(DataTable rows, IReadOnlyList<SqlOrderByColumn> ordering)");
        StringAssert.Contains(source, "Header = column.ColumnName,");
        StringAssert.Contains(source, "SortDirection = FindQuerySortDirection(column.ColumnName, ordering),");
        StringAssert.Contains(source, "LastRowsOrdering = SqlOrderByParser.Parse(statement.Text);");
        StringAssert.Contains(source, "Binding = new Binding($\"[{column.ColumnName}]\")");
    }

    [TestMethod]
    public async Task SessionView_QueryCompletion_IsEmbeddedInsteadOfTopLevelWindow()
    {
        var xaml = await ReadFixtureAsync("SessionView.xaml");
        var source = await ReadFixtureAsync("SessionView.xaml.cs");
        var runQueryStatements = ExtractMethod(source, "private async Task RunQueryStatementsAsync");

        StringAssert.Contains(xaml, "x:Name=\"QueryCompletionOverlay\"");
        StringAssert.Contains(xaml, "x:Name=\"QueryCompletionList\"");
        StringAssert.Contains(xaml, "ClipToBounds=\"False\"");
        StringAssert.Contains(xaml, "IsHitTestVisible=\"False\"");
        StringAssert.Contains(xaml, "SystemColors.HighlightBrushKey");
        StringAssert.Contains(xaml, "Color=\"{StaticResource AccentColor}\"");
        StringAssert.Contains(source, "QueryCompletionList.HandleKey(e);");
        StringAssert.Contains(source, "QueryCompletionList.RequestInsertion(e);");
        StringAssert.Contains(runQueryStatements, "HideCompletionOverlay();");
        Assert.IsTrue(
            runQueryStatements.IndexOf("HideCompletionOverlay();", StringComparison.Ordinal) <
            runQueryStatements.IndexOf("if (_queryCancellation is not null)", StringComparison.Ordinal));
        StringAssert.Contains(source, "Canvas.SetTop(QueryCompletionOverlay, Math.Max(0, overlayPoint.Y));");
        Assert.IsFalse(source.Contains("QueryCompletionLayer.ActualHeight - overlayHeight", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("CompletionWindow", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SessionView_QueryRunner_CanRunSelectedCurrentOrAllStatements()
    {
        var xaml = await ReadFixtureAsync("SessionView.xaml");
        var source = await ReadFixtureAsync("SessionView.xaml.cs");
        var setQueryRunning = ExtractMethod(source, "private void SetQueryRunning");

        StringAssert.Contains(xaml, "x:Name=\"RunQueryButton\" Content=\"Run selected\"");
        StringAssert.Contains(xaml, "x:Name=\"RunAllQueriesButton\" Content=\"Run all\"");
        StringAssert.Contains(source, "private async void RunAllQueries_Click");
        StringAssert.Contains(source, "GetSelectedOrCurrentQueryStatements()");
        StringAssert.Contains(source, "QueryEditor.SelectedText");
        StringAssert.Contains(source, "QueryEditor.SelectionStart");
        StringAssert.Contains(source, "SqlStatementParser.FindStatementAtOffset(QueryEditor.Text, QueryEditor.CaretOffset)");
        StringAssert.Contains(source, "SqlStatementParser.Parse(QueryEditor.Text)");
        StringAssert.Contains(source, "Running {index + 1} of {statements.Count}");
        StringAssert.Contains(setQueryRunning, "RunAllQueriesButton.IsEnabled = !isRunning;");
    }

    [TestMethod]
    public async Task SessionView_QueryRunner_ShowsDmlSummaryWithoutResultGrid()
    {
        var source = await ReadFixtureAsync("SessionView.xaml.cs");
        var executionResult = ExtractMethod(source, "private void ShowQueryExecutionResult");
        var affectedRows = ExtractMethod(source, "private static string FormatAffectedRows");

        AssertInOrder(
            executionResult,
            "if (summary.LastRows is not null)",
            "ShowQueryResults(summary.LastRows, summary.LastRowsOrdering);",
            "return;",
            "HideQueryResults();",
            "QueryStatusText.Text = FormatAffectedRows(summary);");
        StringAssert.Contains(affectedRows, "rows added");
        StringAssert.Contains(affectedRows, "rows modified");
        StringAssert.Contains(affectedRows, "rows deleted");
        StringAssert.Contains(source, "case SqlStatementKind.Insert:");
        StringAssert.Contains(source, "case SqlStatementKind.Update:");
        StringAssert.Contains(source, "case SqlStatementKind.Delete:");
    }

    [TestMethod]
    public async Task SessionView_QueryEditor_RepopulatesDefaultSelectWhenEmptyAfterUserEdits()
    {
        var source = await ReadFixtureAsync("SessionView.xaml.cs");
        var prepopulate = ExtractMethod(source, "private void PrepopulateQueryEditorIfPristine");

        StringAssert.Contains(prepopulate, "if (_queryTextEditedByUser && !string.IsNullOrWhiteSpace(QueryEditor.Text))");
        StringAssert.Contains(prepopulate, "_queryTextEditedByUser = false;");
    }

    [TestMethod]
    public async Task SessionView_BooleanDataGridColumnsUseThemedCheckBoxes()
    {
        var xaml = await ReadFixtureAsync("SessionView.xaml");
        var source = await ReadFixtureAsync("SessionView.xaml.cs");

        StringAssert.Contains(xaml, "ElementStyle=\"{StaticResource DataGridCheckBoxStyle}\"");
        StringAssert.Contains(xaml, "EditingElementStyle=\"{StaticResource DataGridCheckBoxStyle}\"");
        StringAssert.Contains(source, "boundColumn is DataGridCheckBoxColumn checkBoxColumn");
        StringAssert.Contains(source, "TryFindResource(\"DataGridCheckBoxStyle\")");
        StringAssert.Contains(source, "checkBoxColumn.ElementStyle = checkBoxStyle;");
        StringAssert.Contains(source, "checkBoxColumn.EditingElementStyle = checkBoxStyle;");
    }

    private static Task<string> ReadFixtureAsync(string name)
    {
        return File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Missing method: {signature}");

        var nextMethod = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        Assert.IsTrue(nextMethod > start, $"Missing end for method: {signature}");

        return source[start..nextMethod];
    }

    private static void AssertInOrder(string source, params string[] snippets)
    {
        var currentIndex = -1;
        foreach (var snippet in snippets)
        {
            var nextIndex = source.IndexOf(snippet, currentIndex + 1, StringComparison.Ordinal);
            Assert.IsTrue(nextIndex > currentIndex, $"Expected snippet after index {currentIndex}: {snippet}");
            currentIndex = nextIndex;
        }
    }
}
