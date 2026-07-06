namespace Probe.DbEditor.Tests.Themes;

[TestClass]
public sealed class DarkThemeTests
{
    [TestMethod]
    public async Task DarkTheme_ContainsReadableEditingAndPixelScrollingRegressions()
    {
        var themePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DarkTheme.xaml");
        var xaml = await File.ReadAllTextAsync(themePath);

        StringAssert.Contains(xaml, "EditingCellBackgroundBrush");
        StringAssert.Contains(xaml, "Color=\"#E2DFD0\"");
        StringAssert.Contains(xaml, "Property=\"IsEditing\" Value=\"True\"");
        StringAssert.Contains(xaml, "Property=\"VirtualizingPanel.ScrollUnit\" Value=\"Pixel\"");
        StringAssert.Contains(xaml, "Property=\"ScrollViewer.CanContentScroll\" Value=\"True\"");
    }
}
