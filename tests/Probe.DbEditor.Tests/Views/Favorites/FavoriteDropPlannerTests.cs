using Probe.DbEditor.Views.Favorites;

namespace Probe.DbEditor.Tests.Views.Favorites;

[TestClass]
public sealed class FavoriteDropPlannerTests
{
    [TestMethod]
    public void PlacementFromPointerY_UsesTopAndBottomHalvesOfHoveredItem()
    {
        Assert.AreEqual(
            FavoriteDropPreviewPlacement.Before,
            FavoriteDropPlanner.PlacementFromPointerY(0, 40));
        Assert.AreEqual(
            FavoriteDropPreviewPlacement.Before,
            FavoriteDropPlanner.PlacementFromPointerY(20, 40));
        Assert.AreEqual(
            FavoriteDropPreviewPlacement.After,
            FavoriteDropPlanner.PlacementFromPointerY(20.01, 40));
        Assert.AreEqual(
            FavoriteDropPreviewPlacement.After,
            FavoriteDropPlanner.PlacementFromPointerY(40, 40));
    }

    [TestMethod]
    public void TargetIndexForPlacement_InsertsBeforeOrAfterHoveredSibling()
    {
        Assert.AreEqual(
            3,
            FavoriteDropPlanner.TargetIndexForPlacement(3, FavoriteDropPreviewPlacement.Before));
        Assert.AreEqual(
            4,
            FavoriteDropPlanner.TargetIndexForPlacement(3, FavoriteDropPreviewPlacement.After));
    }
}
