using System.Globalization;

namespace Probe.DbEditor.Views.SqlEditor;

public static class SqlStatementParser
{
    public static IReadOnlyList<SqlStatement> Parse(string sql, int baseOffset = 0)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return [];
        }

        var statements = new List<SqlStatement>();
        var start = 0;
        foreach (var separator in SqlTextScanner.FindStatementSeparators(sql))
        {
            AddStatement(statements, sql, start, separator, baseOffset);
            start = separator + 1;
        }

        AddStatement(statements, sql, start, sql.Length, baseOffset);
        return statements;
    }

    public static SqlStatement? FindStatementAtOffset(string sql, int offset)
    {
        var statements = Parse(sql);
        if (statements.Count == 0)
        {
            return null;
        }

        var containingStatement = statements.FirstOrDefault(statement =>
            offset >= statement.StartOffset && offset <= statement.EndOffset);
        if (containingStatement is not null)
        {
            return containingStatement;
        }

        return statements.LastOrDefault(statement => statement.EndOffset <= offset) ??
               statements.FirstOrDefault(statement => statement.StartOffset >= offset);
    }

    private static void AddStatement(List<SqlStatement> statements, string sql, int start, int end, int baseOffset)
    {
        var trimmedStart = start;
        while (trimmedStart < end && char.IsWhiteSpace(sql[trimmedStart]))
        {
            trimmedStart++;
        }

        var trimmedEnd = end;
        while (trimmedEnd > trimmedStart && char.IsWhiteSpace(sql[trimmedEnd - 1]))
        {
            trimmedEnd--;
        }

        if (trimmedStart >= trimmedEnd)
        {
            return;
        }

        var kind = Classify(sql, trimmedStart, trimmedEnd);
        if (kind == SqlStatementKind.None)
        {
            return;
        }

        statements.Add(new SqlStatement(
            sql[trimmedStart..trimmedEnd],
            baseOffset + trimmedStart,
            baseOffset + trimmedEnd,
            kind));
    }

    private static SqlStatementKind Classify(string sql, int start, int end)
    {
        var keywordStart = FindFirstKeywordStart(sql, start, end);
        if (keywordStart < 0)
        {
            return SqlStatementKind.None;
        }

        var keywordEnd = keywordStart;
        while (keywordEnd < end && (char.IsLetter(sql[keywordEnd]) || sql[keywordEnd] == '_'))
        {
            keywordEnd++;
        }

        var keyword = sql[keywordStart..keywordEnd].ToUpper(CultureInfo.InvariantCulture);
        return keyword switch
        {
            "SELECT" or "WITH" => SqlStatementKind.Select,
            "INSERT" or "REPLACE" => SqlStatementKind.Insert,
            "UPDATE" => SqlStatementKind.Update,
            "DELETE" => SqlStatementKind.Delete,
            _ => SqlStatementKind.Other
        };
    }

    private static int FindFirstKeywordStart(string sql, int start, int end)
    {
        return SqlTextScanner.FindFirstKeywordStart(sql, start, end);
    }
}
