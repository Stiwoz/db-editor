namespace Probe.DbEditor.Tests.Interaction;

[TestClass]
public sealed class SessionViewEditCommitTests
{
    [TestMethod]
    public async Task SessionView_CellEditEnding_SchedulesPendingEditCaptureForEnterCommit()
    {
        var source = await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "SessionView.xaml.cs"));
        var cellEditEnding = ExtractMethod(source, "private async void TableDataGrid_CellEditEnding");
        var currentCellChanged = ExtractMethod(source, "private async void TableDataGrid_CurrentCellChanged");

        StringAssert.Contains(cellEditEnding, "_pendingEditCandidate = new PendingEditCandidate(rowView.Row, columnName);");
        StringAssert.Contains(cellEditEnding, "await CapturePendingCellEditAsync();");
        StringAssert.Contains(currentCellChanged, "await CapturePendingCellEditAsync();");
        StringAssert.Contains(source, "private async Task CapturePendingCellEditAsync()");
        StringAssert.Contains(source, "CreateTextEditingContextMenu");
        StringAssert.Contains(source, "ApplicationCommands.Cut");
        StringAssert.Contains(source, "ApplicationCommands.Copy");
        StringAssert.Contains(source, "ApplicationCommands.Paste");
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Missing method: {signature}");

        var nextMethod = source.IndexOf("\n    private ", start + signature.Length, StringComparison.Ordinal);
        Assert.IsTrue(nextMethod > start, $"Missing end for method: {signature}");

        return source[start..nextMethod];
    }
}
