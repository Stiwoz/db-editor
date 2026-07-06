using Probe.DbEditor.Views.SqlEditor;

namespace Probe.DbEditor.Tests.Views.SqlEditor;

[TestClass]
public sealed class MySqlSyntaxHighlightingTests
{
    [TestMethod]
    public void Load_ReturnsMySqlHighlightingDefinition()
    {
        var definition = MySqlSyntaxHighlighting.Load();

        Assert.AreEqual("MySQL", definition.Name);
        Assert.IsTrue(definition.NamedHighlightingColors.Any(color => color.Name == "Keyword"));
        Assert.IsTrue(definition.MainRuleSet.Spans.Count > 0);
    }
}
