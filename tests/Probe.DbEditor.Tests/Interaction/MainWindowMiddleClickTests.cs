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
    public async Task MiddleClickScrollBehavior_StopsWhenMiddleButtonIsReleased()
    {
        var source = await ReadFixtureAsync("MiddleClickScrollBehavior.cs");
        var mouseDownHandler = SliceBetween(
            source,
            "private void OnPreviewMouseDown",
            "private void OnPreviewMouseUp");

        StringAssert.Contains(source, "_root.PreviewMouseUp += OnPreviewMouseUp;");
        StringAssert.Contains(source, "_root.PreviewMouseUp -= OnPreviewMouseUp;");
        StringAssert.Contains(source, "private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)");
        StringAssert.Contains(source, "e.ChangedButton != MouseButton.Middle || !_isActive");
        StringAssert.Contains(source, "Mouse.MiddleButton != MouseButtonState.Pressed");
        Assert.IsFalse(mouseDownHandler.Contains("Stop();", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MiddleClickScrollBehavior_ShiftWheelScrollsHorizontallyWithoutAffectingMiddlePressScroll()
    {
        var source = await ReadFixtureAsync("MiddleClickScrollBehavior.cs");
        var mouseWheelHandler = SliceBetween(
            source,
            "private void OnPreviewMouseWheel",
            "private void OnPreviewKeyDown");

        StringAssert.Contains(source, "private static bool IsShiftPressed()");
        StringAssert.Contains(source, "Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)");
        StringAssert.Contains(source, "FindScrollableViewer(e.OriginalSource, ScrollDirection.Horizontal)");
        StringAssert.Contains(source, "viewer.ScrollToHorizontalOffset(offset);");
        StringAssert.Contains(source, "viewer.HorizontalOffset - ComputeHorizontalWheelDistance(e.Delta)");
        Assert.IsFalse(mouseWheelHandler.Contains("Stop();", StringComparison.Ordinal));
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

    private static string SliceBetween(string source, string startMarker, string endMarker)
    {
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        Assert.IsTrue(startIndex >= 0, $"Missing marker: {startMarker}");
        Assert.IsTrue(endIndex > startIndex, $"Missing marker: {endMarker}");

        return source[startIndex..endIndex];
    }

    private static void AssertOptionToString<TValue>(string nestedTypeName, string label, TValue value)
    {
        var optionType = typeof(MainWindow).GetNestedType(nestedTypeName, BindingFlags.NonPublic);
        Assert.IsNotNull(optionType, $"Missing nested option type: {nestedTypeName}");

        var option = Activator.CreateInstance(optionType, label, value);
        Assert.AreEqual(label, option?.ToString());
    }
}
