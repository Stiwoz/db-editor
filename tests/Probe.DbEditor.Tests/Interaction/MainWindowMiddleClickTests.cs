namespace Probe.DbEditor.Tests.Interaction;

[TestClass]
public sealed class MainWindowMiddleClickTests
{
    [TestMethod]
    public async Task MainWindow_MiddleClickClose_IsScopedToConnectionTabHeader()
    {
        var mainWindowXaml = await ReadFixtureAsync("MainWindow.xaml");
        var mainWindowCode = await ReadFixtureAsync("MainWindow.xaml.cs");

        StringAssert.Contains(
            mainWindowXaml,
            "behaviors:MiddleClickScrollBehavior.IsEnabled=\"True\"");
        StringAssert.Contains(
            mainWindowCode,
            "panel.PreviewMouseDown += ConnectionTabHeader_PreviewMouseDown;");
        StringAssert.DoesNotMatch(
            mainWindowCode,
            new("tab\\.PreviewMouseDown\\s*\\+="));
    }

    private static Task<string> ReadFixtureAsync(string fileName)
    {
        return File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }
}
