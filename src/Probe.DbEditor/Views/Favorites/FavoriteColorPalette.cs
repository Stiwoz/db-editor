using System.Windows.Media;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Views.Favorites;

public static class FavoriteColorPalette
{
    public static Color? ToColor(ConnectionFavoriteColor favoriteColor)
    {
        return favoriteColor switch
        {
            ConnectionFavoriteColor.Gray => Color.FromRgb(0x7F, 0x7F, 0x7F),
            ConnectionFavoriteColor.Green => Color.FromRgb(0x34, 0xC7, 0x59),
            ConnectionFavoriteColor.Purple => Color.FromRgb(0xAF, 0x52, 0xDE),
            ConnectionFavoriteColor.Blue => Color.FromRgb(0x00, 0x7A, 0xFF),
            ConnectionFavoriteColor.Yellow => Color.FromRgb(0xFF, 0xCC, 0x00),
            ConnectionFavoriteColor.Red => Color.FromRgb(0xFF, 0x3B, 0x30),
            ConnectionFavoriteColor.Orange => Color.FromRgb(0xFF, 0x95, 0x00),
            _ => null
        };
    }

    public static Brush CreateSwatchBrush(ConnectionFavoriteColor favoriteColor)
    {
        var color = ToColor(favoriteColor);
        return color is null ? Brushes.Transparent : Freeze(new SolidColorBrush(color.Value));
    }

    public static Brush CreateBackgroundBrush(
        ConnectionFavoriteColor ownColor,
        ConnectionFavoriteColor inheritedColor)
    {
        var color = ToColor(ownColor == ConnectionFavoriteColor.None ? inheritedColor : ownColor);
        if (color is null)
        {
            return Brushes.Transparent;
        }

        var alpha = ownColor == ConnectionFavoriteColor.None ? 0x38 : 0x70;
        return Freeze(new SolidColorBrush(Color.FromArgb((byte)alpha, color.Value.R, color.Value.G, color.Value.B)));
    }

    public static string LabelFor(ConnectionFavoriteColor favoriteColor)
    {
        return favoriteColor switch
        {
            ConnectionFavoriteColor.Gray => "Gray",
            ConnectionFavoriteColor.Green => "Green",
            ConnectionFavoriteColor.Purple => "Purple",
            ConnectionFavoriteColor.Blue => "Blue",
            ConnectionFavoriteColor.Yellow => "Yellow",
            ConnectionFavoriteColor.Red => "Red",
            ConnectionFavoriteColor.Orange => "Orange",
            _ => "No Color"
        };
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
