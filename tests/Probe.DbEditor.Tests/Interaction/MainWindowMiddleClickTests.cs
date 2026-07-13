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
            "header.PreviewMouseDown += ConnectionTabHeader_PreviewMouseDown;");
        StringAssert.DoesNotMatch(
            mainWindowCode,
            new("tab\\.PreviewMouseDown\\s*\\+="));
    }

    [TestMethod]
    public async Task MainWindow_SavedConnectionsUseFolderTreeWithContextMenuDragDropRenameAndColors()
    {
        var mainWindowXaml = await ReadFixtureAsync("MainWindow.xaml");
        var mainWindowCode = await ReadFixtureAsync("MainWindow.xaml.cs");

        StringAssert.Contains(mainWindowXaml, "TreeView x:Name=\"SavedProfilesTree\"");
        StringAssert.Contains(mainWindowXaml, "HierarchicalDataTemplate");
        StringAssert.Contains(mainWindowXaml, "Background=\"{Binding BackgroundBrush}\"");
        StringAssert.Contains(mainWindowXaml, "StrokeDashArray=\"2 2\"");
        StringAssert.Contains(mainWindowXaml, "FavoriteRootDropPreview");
        StringAssert.Contains(mainWindowXaml, "FavoriteDragPreviewPopup");
        StringAssert.Contains(mainWindowXaml, "x:Name=\"DropPreviewLayer\"");
        StringAssert.Contains(mainWindowXaml, "IsHitTestVisible=\"False\"");
        StringAssert.Contains(mainWindowXaml, "IsHitTestVisible=\"True\"");
        StringAssert.Contains(mainWindowXaml, "ProfileColorList");
        StringAssert.Contains(mainWindowXaml, "FolderDetailPanel");
        StringAssert.Contains(mainWindowXaml, "FolderColorList");
        StringAssert.Contains(mainWindowXaml, "SaveFolder_Click");
        StringAssert.Contains(mainWindowXaml, "FavoriteContextColorMenu");
        StringAssert.Contains(mainWindowXaml, "FavoriteContextConnectMenuItem");
        StringAssert.Contains(mainWindowXaml, "FavoriteContextDuplicateMenuItem");
        StringAssert.Contains(mainWindowXaml, "FavoriteContextDeleteMenuItem");
        StringAssert.Contains(mainWindowXaml, "FavoriteContextNewProfile_Click");
        StringAssert.Contains(mainWindowXaml, "FavoriteDropPreviewPlacement.Before");
        StringAssert.Contains(mainWindowXaml, "FavoriteDropPreviewPlacement.After");
        StringAssert.Contains(mainWindowXaml, "FavoriteDropPreviewPlacement.Inside");
        StringAssert.Contains(mainWindowXaml, "FavoriteColorListBoxItemStyle");
        Assert.IsFalse(mainWindowXaml.Contains("CornerRadius=\"4\"", StringComparison.Ordinal));
        Assert.IsTrue(
            mainWindowXaml.IndexOf("FavoriteContextColorMenu", StringComparison.Ordinal) <
            mainWindowXaml.IndexOf("FavoriteContextConnect_Click", StringComparison.Ordinal));
        Assert.IsTrue(
            mainWindowXaml.IndexOf("ItemsPresenter x:Name=\"ItemsHost\"", StringComparison.Ordinal) <
            mainWindowXaml.IndexOf("x:Name=\"DropPreviewLayer\"", StringComparison.Ordinal));
        var dropPreviewLayer = SliceBetween(
            mainWindowXaml,
            "x:Name=\"DropPreviewLayer\"",
            "x:Name=\"DropPreviewBefore\"");
        StringAssert.Contains(dropPreviewLayer, "IsHitTestVisible=\"False\"");
        StringAssert.Contains(dropPreviewLayer, "Panel.ZIndex=\"1\"");
        StringAssert.Contains(mainWindowCode, "LoadFavoritesAsync()");
        StringAssert.Contains(mainWindowCode, "SaveFavoritesAsync()");
        StringAssert.Contains(mainWindowCode, "DragDrop.DoDragDrop");
        StringAssert.Contains(mainWindowCode, "ClearFavoriteDragCandidate();");
        StringAssert.Contains(mainWindowCode, "SavedProfilesContextMenu_Closed");
        StringAssert.Contains(mainWindowCode, "_favoriteDragArmed");
        StringAssert.Contains(mainWindowCode, "_favoriteDropPlacement");
        StringAssert.Contains(mainWindowCode, "_isFavoriteRootDropTarget");
        StringAssert.Contains(mainWindowCode, "StartFavoriteDragPreview");
        StringAssert.Contains(mainWindowCode, "SetFavoriteDropTarget");
        StringAssert.Contains(mainWindowCode, "ReferenceEquals(_favoriteDropTarget, dropTarget.PreviewItem)");
        StringAssert.Contains(mainWindowCode, "FavoriteDragPayload");
        StringAssert.Contains(mainWindowCode, "ResolveFavoriteDropTarget");
        StringAssert.Contains(mainWindowCode, "ResolveFavoriteDropPlacement");
        StringAssert.Contains(mainWindowCode, "TryResolveCachedProfileDropTarget");
        StringAssert.Contains(mainWindowCode, "TryResolveCachedFolderDropTarget");
        StringAssert.Contains(mainWindowCode, "IsPointerWithinCachedDropPreviewBand");
        StringAssert.Contains(mainWindowCode, "IsPointerWithinElementBounds");
        StringAssert.Contains(mainWindowCode, "\"DropPreviewBefore\"");
        StringAssert.Contains(mainWindowCode, "\"DropPreviewAfter\"");
        StringAssert.Contains(mainWindowCode, "FavoriteDropPreviewHitBuffer");
        StringAssert.Contains(mainWindowCode, "FindVisualDescendantByName<FrameworkElement>(targetTreeItem, \"ItemRow\")");
        StringAssert.Contains(mainWindowCode, "rowPosition.Y < 0 || rowPosition.Y > itemRow.ActualHeight");
        StringAssert.Contains(mainWindowCode, "ResolveProfileDropTarget");
        StringAssert.Contains(mainWindowCode, "ResolveFolderDropTarget");
        StringAssert.Contains(mainWindowCode, "MoveFavoriteToDropTarget");
        StringAssert.Contains(mainWindowCode, "MoveProfileToDropTarget");
        StringAssert.Contains(mainWindowCode, "MoveFolderToDropTarget");
        StringAssert.Contains(mainWindowCode, "FindFavoriteTreeViewItem");
        StringAssert.Contains(mainWindowCode, "IsDragged = true");
        StringAssert.Contains(mainWindowCode, "CaptureExpandedFolderIds");
        StringAssert.Contains(mainWindowCode, "ConfirmDeleteFavorite");
        StringAssert.Contains(mainWindowCode, "MessageBox.Show");
        StringAssert.Contains(mainWindowCode, "DuplicateProfileAsync");
        StringAssert.Contains(mainWindowCode, "LoadFolder");
        StringAssert.Contains(mainWindowCode, "FinishFavoriteRenameAsync");
        StringAssert.Contains(mainWindowCode, "CreateConnectionTabHeader(profile, tab)");
        StringAssert.Contains(mainWindowCode, "CreateConnectionTabHeaderBrush");
        StringAssert.Contains(mainWindowCode, "Padding = new Thickness(0)");
        StringAssert.Contains(mainWindowCode, "new Border");
        StringAssert.Contains(mainWindowCode, "Padding = new Thickness(12, 6, 12, 6)");
        StringAssert.Contains(mainWindowCode, "FavoriteColorPalette.CreateBackgroundBrush(profile.Color");
        StringAssert.Contains(mainWindowCode, "FavoriteColorPalette.CreateSwatchBrush");
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
