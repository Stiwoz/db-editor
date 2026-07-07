using System.Reflection;
using Probe.DbEditor;
using Probe.DbEditor.Models;

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

    [TestMethod]
    public void MainWindow_ConnectionOptionModels_RenderReadableLabels()
    {
        AssertOptionToString("ProtocolOption", "TCP/IP", ConnectionProtocolKind.Tcp);
        AssertOptionToString("TlsOption", "Verify full certificate", DatabaseTlsMode.VerifyFull);
    }

    private static Task<string> ReadFixtureAsync(string fileName)
    {
        return File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private static void AssertOptionToString<TValue>(string nestedTypeName, string label, TValue value)
    {
        var optionType = typeof(MainWindow).GetNestedType(nestedTypeName, BindingFlags.NonPublic);
        Assert.IsNotNull(optionType, $"Missing nested option type: {nestedTypeName}");

        var option = Activator.CreateInstance(optionType, label, value);
        Assert.AreEqual(label, option?.ToString());
    }
}
