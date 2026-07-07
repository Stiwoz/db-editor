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
        var state = ScannerState.Normal;

        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            var next = index + 1 < sql.Length ? sql[index + 1] : '\0';

            switch (state)
            {
                case ScannerState.Normal:
                    if (current == '\'')
                    {
                        state = ScannerState.SingleQuotedString;
                    }
                    else if (current == '"')
                    {
                        state = ScannerState.DoubleQuotedString;
                    }
                    else if (current == '`')
                    {
                        state = ScannerState.BacktickIdentifier;
                    }
                    else if (current == '-' && next == '-')
                    {
                        state = ScannerState.LineComment;
                        index++;
                    }
                    else if (current == '#')
                    {
                        state = ScannerState.LineComment;
                    }
                    else if (current == '/' && next == '*')
                    {
                        state = ScannerState.BlockComment;
                        index++;
                    }
                    else if (current == ';')
                    {
                        AddStatement(statements, sql, start, index, baseOffset);
                        start = index + 1;
                    }

                    break;

                case ScannerState.SingleQuotedString:
                    if (current == '\\' && next != '\0')
                    {
                        index++;
                    }
                    else if (current == '\'' && next == '\'')
                    {
                        index++;
                    }
                    else if (current == '\'')
                    {
                        state = ScannerState.Normal;
                    }

                    break;

                case ScannerState.DoubleQuotedString:
                    if (current == '\\' && next != '\0')
                    {
                        index++;
                    }
                    else if (current == '"' && next == '"')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = ScannerState.Normal;
                    }

                    break;

                case ScannerState.BacktickIdentifier:
                    if (current == '`' && next == '`')
                    {
                        index++;
                    }
                    else if (current == '`')
                    {
                        state = ScannerState.Normal;
                    }

                    break;

                case ScannerState.LineComment:
                    if (current is '\r' or '\n')
                    {
                        state = ScannerState.Normal;
                    }

                    break;

                case ScannerState.BlockComment:
                    if (current == '*' && next == '/')
                    {
                        state = ScannerState.Normal;
                        index++;
                    }

                    break;
            }
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
        var index = start;
        while (index < end)
        {
            if (char.IsWhiteSpace(sql[index]))
            {
                index++;
                continue;
            }

            if (sql[index] == '-' && index + 1 < end && sql[index + 1] == '-')
            {
                index = SkipLineComment(sql, index + 2, end);
                continue;
            }

            if (sql[index] == '#')
            {
                index = SkipLineComment(sql, index + 1, end);
                continue;
            }

            if (sql[index] == '/' && index + 1 < end && sql[index + 1] == '*')
            {
                index = SkipBlockComment(sql, index + 2, end);
                continue;
            }

            return char.IsLetter(sql[index]) ? index : -1;
        }

        return -1;
    }

    private static int SkipLineComment(string sql, int start, int end)
    {
        var index = start;
        while (index < end && sql[index] is not '\r' and not '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipBlockComment(string sql, int start, int end)
    {
        var index = start;
        while (index + 1 < end)
        {
            if (sql[index] == '*' && sql[index + 1] == '/')
            {
                return index + 2;
            }

            index++;
        }

        return end;
    }

    private enum ScannerState
    {
        Normal,
        SingleQuotedString,
        DoubleQuotedString,
        BacktickIdentifier,
        LineComment,
        BlockComment
    }
}
