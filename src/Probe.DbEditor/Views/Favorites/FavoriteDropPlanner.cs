namespace Probe.DbEditor.Views.Favorites;

internal static class FavoriteDropPlanner
{
    public static FavoriteDropPreviewPlacement PlacementFromPointerY(double pointerY, double itemHeight)
    {
        var height = Math.Max(1, itemHeight);
        return pointerY <= height / 2
            ? FavoriteDropPreviewPlacement.Before
            : FavoriteDropPreviewPlacement.After;
    }

    public static int TargetIndexForPlacement(
        int siblingIndex,
        FavoriteDropPreviewPlacement placement)
    {
        return siblingIndex + (placement == FavoriteDropPreviewPlacement.After ? 1 : 0);
    }
}
