using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Probe.DbEditor.Views.SqlEditor;

public static class MySqlSyntaxHighlighting
{
    private const string Definition = """
        <?xml version="1.0"?>
        <SyntaxDefinition name="MySQL" extensions=".sql" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
          <Color name="Comment" foreground="#8F877A" />
          <Color name="String" foreground="#D6B98E" />
          <Color name="Number" foreground="#B8C7A7" />
          <Color name="Keyword" foreground="#D99A6C" fontWeight="bold" />
          <Color name="Function" foreground="#BFA9D6" />
          <Color name="Variable" foreground="#AFC7D6" />
          <Color name="Operator" foreground="#C8C1B2" />

          <RuleSet>
            <Span color="Comment" begin="/\*" end="\*/" multiline="true" />
            <Span color="Comment" begin="--" end="$" />
            <Span color="Comment" begin="#" end="$" />
            <Span color="String" begin="'" end="'" />
            <Span color="String" begin="&quot;" end="&quot;" />
            <Span color="String" begin="`" end="`" />

            <Keywords color="Keyword">
              <Word>ADD</Word><Word>ALTER</Word><Word>AND</Word><Word>AS</Word><Word>ASC</Word><Word>BETWEEN</Word>
              <Word>BY</Word><Word>CASE</Word><Word>CREATE</Word><Word>DELETE</Word><Word>DESC</Word><Word>DISTINCT</Word>
              <Word>DROP</Word><Word>ELSE</Word><Word>END</Word><Word>EXISTS</Word><Word>FROM</Word><Word>GROUP</Word>
              <Word>HAVING</Word><Word>IF</Word><Word>IN</Word><Word>INDEX</Word><Word>INNER</Word><Word>INSERT</Word>
              <Word>INTO</Word><Word>IS</Word><Word>JOIN</Word><Word>KEY</Word><Word>LEFT</Word><Word>LIKE</Word>
              <Word>LIMIT</Word><Word>NOT</Word><Word>NULL</Word><Word>ON</Word><Word>OR</Word><Word>ORDER</Word>
              <Word>OUTER</Word><Word>PRIMARY</Word><Word>RIGHT</Word><Word>SELECT</Word><Word>SET</Word><Word>TABLE</Word>
              <Word>THEN</Word><Word>UNION</Word><Word>UNIQUE</Word><Word>UPDATE</Word><Word>VALUES</Word><Word>WHEN</Word>
              <Word>WHERE</Word>
            </Keywords>

            <Keywords color="Function">
              <Word>AVG</Word><Word>COALESCE</Word><Word>CONCAT</Word><Word>COUNT</Word><Word>DATE</Word><Word>IFNULL</Word>
              <Word>LOWER</Word><Word>MAX</Word><Word>MIN</Word><Word>NOW</Word><Word>SUM</Word><Word>UPPER</Word>
            </Keywords>

            <Rule color="Variable">[\@:][A-Za-z_][A-Za-z0-9_]*</Rule>
            <Rule color="Number">\b[0-9]+(\.[0-9]+)?\b</Rule>
            <Rule color="Operator">[=+\-*/&lt;&gt;!]+</Rule>
          </RuleSet>
        </SyntaxDefinition>
        """;

    public static IHighlightingDefinition Load()
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(Definition));
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            return HighlightingManager.Instance.GetDefinition("TSQL") ??
                   HighlightingManager.Instance.GetDefinition("XML") ??
                   throw new InvalidOperationException("AvalonEdit did not provide a fallback syntax definition.");
        }
    }
}
