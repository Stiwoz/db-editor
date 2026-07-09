using System.Windows.Media;
using Probe.DbEditor.Models;
using Probe.DbEditor.Views.Favorites;

namespace Probe.DbEditor.Tests.Views.Favorites;

[TestClass]
public sealed class FavoriteColorPaletteTests
{
    [TestMethod]
    public void ToColor_UsesRequestedMacOsPalette()
    {
        AssertColor(ConnectionFavoriteColor.Gray, "#7F7F7F");
        AssertColor(ConnectionFavoriteColor.Green, "#34C759");
        AssertColor(ConnectionFavoriteColor.Purple, "#AF52DE");
        AssertColor(ConnectionFavoriteColor.Blue, "#007AFF");
        AssertColor(ConnectionFavoriteColor.Yellow, "#FFCC00");
        AssertColor(ConnectionFavoriteColor.Red, "#FF3B30");
        AssertColor(ConnectionFavoriteColor.Orange, "#FF9500");
        Assert.IsNull(FavoriteColorPalette.ToColor(ConnectionFavoriteColor.None));
    }

    [TestMethod]
    public void CreateBackgroundBrush_MakesInheritedFolderColorMoreTransparent()
    {
        var directBrush = AssertSolidBrush(FavoriteColorPalette.CreateBackgroundBrush(
            ConnectionFavoriteColor.Blue,
            ConnectionFavoriteColor.None));
        var inheritedBrush = AssertSolidBrush(FavoriteColorPalette.CreateBackgroundBrush(
            ConnectionFavoriteColor.None,
            ConnectionFavoriteColor.Blue));

        Assert.IsTrue(directBrush.Color.A > inheritedBrush.Color.A);
        Assert.AreEqual(directBrush.Color.R, inheritedBrush.Color.R);
        Assert.AreEqual(directBrush.Color.G, inheritedBrush.Color.G);
        Assert.AreEqual(directBrush.Color.B, inheritedBrush.Color.B);
    }

    private static void AssertColor(ConnectionFavoriteColor favoriteColor, string expected)
    {
        Assert.AreEqual(
            (Color)ColorConverter.ConvertFromString(expected),
            FavoriteColorPalette.ToColor(favoriteColor));
    }

    private static SolidColorBrush AssertSolidBrush(Brush brush)
    {
        Assert.IsInstanceOfType<SolidColorBrush>(brush);
        return (SolidColorBrush)brush;
    }
}
