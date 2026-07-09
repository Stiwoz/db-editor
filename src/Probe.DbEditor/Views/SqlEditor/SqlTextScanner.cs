using System.Text;

namespace Probe.DbEditor.Views.SqlEditor;

internal static class SqlTextScanner
{
    public static IEnumerable<int> FindStatementSeparators(string sql)
    {
        var separators = new List<int>();
        ScanCode(sql, 0, sql.Length, point =>
        {
            if (point.Character == ';')
            {
                separators.Add(point.Index);
            }

            return false;
        });

        return separators;
    }

    public static int FindFirstKeywordStart(string sql, int start, int end)
    {
        var keywordStart = -1;
        ScanCode(sql, start, end, point =>
        {
            if (char.IsWhiteSpace(point.Character))
            {
                return false;
            }

            keywordStart = char.IsLetter(point.Character) ? point.Index : -1;
            return true;
        });

        return keywordStart;
    }

    public static int FindTopLevelKeywordSequenceStart(string sql, string firstKeyword, string secondKeyword)
    {
        var sequenceStart = -1;
        ScanCode(sql, 0, sql.Length, point =>
        {
            if (point.Depth != 0 || !IsKeywordAt(sql, point.Index, firstKeyword))
            {
                return false;
            }

            var secondKeywordIndex = SkipWhitespace(sql, point.Index + firstKeyword.Length);
            if (!IsKeywordAt(sql, secondKeywordIndex, secondKeyword))
            {
                return false;
            }

            sequenceStart = SkipWhitespace(sql, secondKeywordIndex + secondKeyword.Length);
            return true;
        });

        return sequenceStart;
    }

    public static int FindTopLevelClauseEnd(string sql, int start, IReadOnlyList<string> terminators)
    {
        var clauseEnd = sql.Length;
        ScanCode(sql, start, sql.Length, point =>
        {
            if (point.Depth != 0)
            {
                return false;
            }

            if (point.Character == ';' || terminators.Any(keyword => IsKeywordAt(sql, point.Index, keyword)))
            {
                clauseEnd = point.Index;
                return true;
            }

            return false;
        });

        return clauseEnd;
    }

    public static IEnumerable<string> SplitTopLevel(string sql, int start, int end, char delimiter)
    {
        var items = new List<string>();
        var itemStart = start;
        ScanCode(sql, start, end, point =>
        {
            if (point.Depth != 0 || point.Character != delimiter)
            {
                return false;
            }

            items.Add(sql[itemStart..point.Index]);
            itemStart = point.Index + 1;
            return false;
        });

        items.Add(sql[itemStart..end]);
        return items;
    }

    public static string? ReadIdentifier(string text, ref int index)
    {
        if (index >= text.Length)
        {
            return null;
        }

        if (text[index] == '`')
        {
            return ReadBacktickIdentifier(text, ref index);
        }

        if (!IsIdentifierStart(text[index]))
        {
            return null;
        }

        var start = index;
        index++;
        while (index < text.Length && IsIdentifierPart(text[index]))
        {
            index++;
        }

        return text[start..index];
    }

    public static bool TryReadKeyword(string text, ref int index, string keyword)
    {
        if (!IsKeywordAt(text, index, keyword))
        {
            return false;
        }

        index += keyword.Length;
        return true;
    }

    public static bool IsKeywordAt(string text, int index, string keyword)
    {
        if (index < 0 || index + keyword.Length > text.Length)
        {
            return false;
        }

        if (!string.Equals(text[index..(index + keyword.Length)], keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var before = index > 0 ? text[index - 1] : '\0';
        var afterIndex = index + keyword.Length;
        var after = afterIndex < text.Length ? text[afterIndex] : '\0';
        return !IsIdentifierPart(before) && !IsIdentifierPart(after);
    }

    public static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    public static void SkipWhitespace(string text, ref int index)
    {
        index = SkipWhitespace(text, index);
    }

    public static bool IsIdentifierPart(char character)
    {
        return char.IsLetterOrDigit(character) || character is '_' or '$';
    }

    private static void ScanCode(
        string sql,
        int start,
        int end,
        Func<SqlCodePoint, bool> visit)
    {
        var state = ScannerState.Normal;
        var depth = 0;
        for (var index = start; index < end; index++)
        {
            var current = sql[index];
            var next = index + 1 < end ? sql[index + 1] : '\0';

            switch (state)
            {
                case ScannerState.Normal:
                    if (current == '\'')
                    {
                        state = ScannerState.SingleQuotedString;
                        continue;
                    }

                    if (current == '"')
                    {
                        state = ScannerState.DoubleQuotedString;
                        continue;
                    }

                    if (current == '`')
                    {
                        state = ScannerState.BacktickIdentifier;
                        continue;
                    }

                    if (current == '-' && next == '-')
                    {
                        state = ScannerState.LineComment;
                        index++;
                        continue;
                    }

                    if (current == '#')
                    {
                        state = ScannerState.LineComment;
                        continue;
                    }

                    if (current == '/' && next == '*')
                    {
                        state = ScannerState.BlockComment;
                        index++;
                        continue;
                    }

                    if (current == '(')
                    {
                        depth++;
                    }
                    else if (current == ')' && depth > 0)
                    {
                        depth--;
                    }

                    if (visit(new SqlCodePoint(index, current, depth)))
                    {
                        return;
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
    }

    private static string? ReadBacktickIdentifier(string text, ref int index)
    {
        var builder = new StringBuilder();
        index++;

        while (index < text.Length)
        {
            var current = text[index];
            var next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (current == '`' && next == '`')
            {
                builder.Append('`');
                index += 2;
                continue;
            }

            if (current == '`')
            {
                index++;
                return builder.ToString();
            }

            builder.Append(current);
            index++;
        }

        return null;
    }

    private static bool IsIdentifierStart(char character)
    {
        return char.IsLetter(character) || character is '_' or '$';
    }

    private readonly record struct SqlCodePoint(int Index, char Character, int Depth);

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
