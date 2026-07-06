using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Views.SqlEditor;

public sealed partial class SqlCompletionService
{
    private readonly Dictionary<string, SqlCompletionItem> _items = new(StringComparer.OrdinalIgnoreCase);

    public SqlCompletionService()
    {
        foreach (var keyword in Keywords)
        {
            Add(keyword, "keyword", 10);
        }

        foreach (var function in Functions)
        {
            Add(function, "function", 9);
        }
    }

    public void AddSchema(string schemaName)
    {
        Add(QuoteIfNeeded(schemaName), "schema", 8);
    }

    public void AddTable(string schemaName, string tableName)
    {
        var safeTableName = QuoteIfNeeded(tableName);
        Add(safeTableName, "table", 8);
        Add($"{QuoteIfNeeded(schemaName)}.{safeTableName}", "table", 8);
    }

    public void AddColumn(string tableName, string columnName)
    {
        var safeColumnName = QuoteIfNeeded(columnName);
        Add(safeColumnName, "column", 7);
        Add($"{QuoteIfNeeded(tableName)}.{safeColumnName}", "column", 7);
    }

    public IReadOnlyList<SqlCompletionData> BuildCompletionData(string prefix, string queryText)
    {
        foreach (Match match in VariableRegex().Matches(queryText))
        {
            Add(match.Value, "variable", 6);
        }

        return _items.Values
            .Where(item => string.IsNullOrWhiteSpace(prefix) ||
                           item.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                           item.Text.Contains($".{prefix}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .Select(item => new SqlCompletionData(item.Text, item.Kind, item.Priority))
            .ToList();
    }

    public static string GetPrefix(TextDocument document, int caretOffset)
    {
        var start = GetCompletionStartOffset(document, caretOffset);
        return document.GetText(start, caretOffset - start);
    }

    public static int GetCompletionStartOffset(TextDocument document, int caretOffset)
    {
        var offset = caretOffset;
        while (offset > 0)
        {
            var character = document.GetCharAt(offset - 1);
            if (!IsCompletionCharacter(character))
            {
                break;
            }

            offset--;
        }

        return offset;
    }

    private void Add(string text, string kind, double priority)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _items[text] = new SqlCompletionItem(text, kind, priority);
    }

    private static string QuoteIfNeeded(string identifier)
    {
        return SimpleIdentifierRegex().IsMatch(identifier) ? identifier : SqlIdentifier.Quote(identifier);
    }

    public static bool IsCompletionCharacter(char character)
    {
        return char.IsLetterOrDigit(character) ||
               character is '_' or '$' or '@' or ':' or '.' or '`';
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$]*$")]
    private static partial Regex SimpleIdentifierRegex();

    [GeneratedRegex(@"[@:][A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex VariableRegex();

    private sealed record SqlCompletionItem(string Text, string Kind, double Priority);

    private static readonly string[] Keywords =
    [
        "ALTER", "AND", "ASC", "BETWEEN", "BY", "CREATE", "DELETE", "DESC", "DISTINCT",
        "DROP", "FROM", "GROUP", "HAVING", "IN", "INDEX", "INNER", "INSERT", "INTO",
        "IS", "JOIN", "LEFT", "LIKE", "LIMIT", "NOT", "NULL", "ON", "OR", "ORDER",
        "OUTER", "PRIMARY", "RIGHT", "SELECT", "SET", "TABLE", "UNION", "UNIQUE",
        "UPDATE", "VALUES", "WHERE"
    ];

    private static readonly string[] Functions =
    [
        "AVG()", "COALESCE()", "CONCAT()", "COUNT()", "DATE()", "IFNULL()", "LOWER()",
        "MAX()", "MIN()", "NOW()", "SUM()", "UPPER()"
    ];
}
