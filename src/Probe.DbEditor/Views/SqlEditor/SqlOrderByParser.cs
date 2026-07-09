using System.ComponentModel;
namespace Probe.DbEditor.Views.SqlEditor;

public static class SqlOrderByParser
{
    private static readonly string[] ClauseTerminators =
    [
        "LIMIT",
        "OFFSET",
        "FETCH",
        "FOR",
        "LOCK",
        "PROCEDURE",
        "UNION",
        "EXCEPT",
        "INTERSECT",
        "INTO"
    ];

    public static IReadOnlyList<SqlOrderByColumn> Parse(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return [];
        }

        var orderByStart = SqlTextScanner.FindTopLevelKeywordSequenceStart(sql, "ORDER", "BY");
        if (orderByStart < 0)
        {
            return [];
        }

        var orderByEnd = SqlTextScanner.FindTopLevelClauseEnd(sql, orderByStart, ClauseTerminators);
        var columns = new List<SqlOrderByColumn>();
        foreach (var item in SqlTextScanner.SplitTopLevel(sql, orderByStart, orderByEnd, ','))
        {
            var column = ParseOrderItem(item);
            if (column is not null)
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    private static SqlOrderByColumn? ParseOrderItem(string item)
    {
        var index = 0;
        var identifiers = new List<string>();

        SqlTextScanner.SkipWhitespace(item, ref index);
        var firstIdentifier = SqlTextScanner.ReadIdentifier(item, ref index);
        if (firstIdentifier is null)
        {
            return null;
        }

        identifiers.Add(firstIdentifier);
        while (true)
        {
            SqlTextScanner.SkipWhitespace(item, ref index);
            if (index >= item.Length || item[index] != '.')
            {
                break;
            }

            index++;
            SqlTextScanner.SkipWhitespace(item, ref index);
            var identifier = SqlTextScanner.ReadIdentifier(item, ref index);
            if (identifier is null)
            {
                return null;
            }

            identifiers.Add(identifier);
        }

        var direction = ListSortDirection.Ascending;
        SqlTextScanner.SkipWhitespace(item, ref index);
        if (index < item.Length)
        {
            if (SqlTextScanner.TryReadKeyword(item, ref index, "ASC"))
            {
                direction = ListSortDirection.Ascending;
            }
            else if (SqlTextScanner.TryReadKeyword(item, ref index, "DESC"))
            {
                direction = ListSortDirection.Descending;
            }
            else
            {
                return null;
            }

            SqlTextScanner.SkipWhitespace(item, ref index);
            if (index < item.Length)
            {
                return null;
            }
        }

        var columnName = identifiers[^1];
        return string.IsNullOrWhiteSpace(columnName)
            ? null
            : new SqlOrderByColumn(columnName, direction);
    }
}
