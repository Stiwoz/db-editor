namespace Probe.DbEditor.Tests.Themes;

[TestClass]
public sealed class DarkThemeTests
{
    [TestMethod]
    public async Task DarkTheme_ContainsReadableEditingAndPixelScrollingRegressions()
    {
        var themePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DarkTheme.xaml");
        var sessionViewPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SessionView.xaml");
        var xaml = await File.ReadAllTextAsync(themePath);
        var sessionViewXaml = await File.ReadAllTextAsync(sessionViewPath);
        var textBoxStyle = ExtractStyle(xaml, "<Style x:Key=\"BaseTextBoxStyle\" TargetType=\"{x:Type TextBox}\">");
        var passwordBoxStyle = ExtractStyle(xaml, "<Style x:Key=\"BasePasswordBoxStyle\" TargetType=\"{x:Type PasswordBox}\">");
        var comboBoxStyle = ExtractStyle(xaml, "<Style x:Key=\"BaseComboBoxStyle\" TargetType=\"{x:Type ComboBox}\">");
        var comboBoxItemStyle = ExtractStyle(xaml, "<Style TargetType=\"{x:Type ComboBoxItem}\">");
        var checkBoxStyle = ExtractStyle(xaml, "<Style x:Key=\"BaseCheckBoxStyle\" TargetType=\"{x:Type CheckBox}\">");
        var dataGridCheckBoxStyle = ExtractStyle(xaml, "<Style x:Key=\"DataGridCheckBoxStyle\"");
        var listBoxItemStyle = ExtractStyle(xaml, "<Style TargetType=\"{x:Type ListBoxItem}\">");
        var separatorStyle = ExtractStyle(xaml, "<Style x:Key=\"SeparatorStyle\" TargetType=\"Separator\">");
        var contextMenuStyle = ExtractStyle(xaml, "<Style TargetType=\"{x:Type ContextMenu}\">");
        var menuItemStyle = ExtractStyle(xaml, "<Style TargetType=\"{x:Type MenuItem}\">");
        var groupBoxStyle = ExtractStyle(xaml, "<Style TargetType=\"{x:Type GroupBox}\">");
        var pendingEditsGroupBoxStyle = ExtractStyle(xaml, "<Style x:Key=\"PendingEditsGroupBoxStyle\"");
        var dataGridColumnHeaderStyle = ExtractStyle(xaml, "<Style TargetType=\"{x:Type DataGridColumnHeader}\">");

        StringAssert.Contains(xaml, "EditingCellBackgroundBrush");
        StringAssert.Contains(xaml, "Color=\"#E2DFD0\"");
        StringAssert.Contains(xaml, "Property=\"IsEditing\" Value=\"True\"");
        StringAssert.Contains(textBoxStyle, "Property=\"FocusVisualStyle\" Value=\"{x:Null}\"");
        StringAssert.Contains(textBoxStyle, "Property=\"IsKeyboardFocusWithin\" Value=\"True\"");
        StringAssert.Contains(textBoxStyle, "ApplicationCommands.Cut");
        StringAssert.Contains(textBoxStyle, "ApplicationCommands.Copy");
        StringAssert.Contains(textBoxStyle, "ApplicationCommands.Paste");
        StringAssert.Contains(passwordBoxStyle, "Property=\"FocusVisualStyle\" Value=\"{x:Null}\"");
        StringAssert.Contains(passwordBoxStyle, "Property=\"IsKeyboardFocusWithin\" Value=\"True\"");
        StringAssert.Contains(comboBoxStyle, "Property=\"FocusVisualStyle\" Value=\"{x:Null}\"");
        StringAssert.Contains(comboBoxStyle, "Property=\"IsKeyboardFocusWithin\" Value=\"True\"");
        StringAssert.Contains(comboBoxStyle, "CornerRadius=\"0\"");
        StringAssert.Contains(comboBoxItemStyle, "CornerRadius=\"0\"");
        StringAssert.Contains(checkBoxStyle, "ControlTemplate TargetType=\"{x:Type CheckBox}\"");
        StringAssert.Contains(checkBoxStyle, "x:Name=\"CheckBorder\"");
        StringAssert.Contains(checkBoxStyle, "x:Name=\"CheckMark\"");
        StringAssert.Contains(checkBoxStyle, "Value=\"{StaticResource AccentBrush}\"");
        StringAssert.Contains(dataGridCheckBoxStyle, "BasedOn=\"{StaticResource BaseCheckBoxStyle}\"");
        StringAssert.Contains(dataGridCheckBoxStyle, "Property=\"HorizontalAlignment\" Value=\"Center\"");
        StringAssert.Contains(listBoxItemStyle, "ControlTemplate TargetType=\"{x:Type ListBoxItem}\"");
        StringAssert.Contains(listBoxItemStyle, "TargetName=\"ItemBorder\" Property=\"Background\"");
        StringAssert.Contains(listBoxItemStyle, "Value=\"{StaticResource AccentBrush}\"");
        StringAssert.Contains(separatorStyle, "Margin=\"28,5,8,5\"");
        StringAssert.Contains(contextMenuStyle, "ControlTemplate TargetType=\"{x:Type ContextMenu}\"");
        StringAssert.Contains(contextMenuStyle, "Background=\"{TemplateBinding Background}\"");
        StringAssert.Contains(contextMenuStyle, "BorderBrush=\"{TemplateBinding BorderBrush}\"");
        StringAssert.Contains(menuItemStyle, "ControlTemplate TargetType=\"{x:Type MenuItem}\"");
        StringAssert.Contains(menuItemStyle, "x:Name=\"PART_Popup\"");
        StringAssert.Contains(menuItemStyle, "IsSubmenuOpen");
        StringAssert.Contains(menuItemStyle, "x:Name=\"SubmenuArrow\"");
        StringAssert.Contains(menuItemStyle, "Value=\"{StaticResource SelectionSoftBrush}\"");
        StringAssert.Contains(groupBoxStyle, "BorderThickness=\"1,1,1,0\"");
        StringAssert.Contains(groupBoxStyle, "BorderThickness=\"1\"");
        StringAssert.Contains(pendingEditsGroupBoxStyle, "BorderThickness=\"0\"");
        StringAssert.Contains(dataGridColumnHeaderStyle, "x:Name=\"AscendingSortIcon\"");
        StringAssert.Contains(dataGridColumnHeaderStyle, "x:Name=\"DescendingSortIcon\"");
        StringAssert.Contains(dataGridColumnHeaderStyle, "Property=\"SortDirection\"");
        StringAssert.Contains(dataGridColumnHeaderStyle, "Value=\"Ascending\"");
        StringAssert.Contains(dataGridColumnHeaderStyle, "Value=\"Descending\"");
        StringAssert.Contains(sessionViewXaml, "Style=\"{StaticResource PendingEditsGroupBoxStyle}\"");
        StringAssert.Contains(xaml, "Property=\"VirtualizingPanel.ScrollUnit\" Value=\"Pixel\"");
        StringAssert.Contains(xaml, "Property=\"ScrollViewer.CanContentScroll\" Value=\"True\"");
    }

    private static string ExtractStyle(string xaml, string styleStart)
    {
        var start = xaml.IndexOf(styleStart, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"Missing style: {styleStart}");

        var end = xaml.IndexOf("\n    <Style", start + styleStart.Length, StringComparison.Ordinal);
        Assert.IsTrue(end > start, $"Missing end for style: {styleStart}");

        return xaml[start..end];
    }
}
