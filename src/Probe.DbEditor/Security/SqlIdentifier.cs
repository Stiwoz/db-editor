using System.Text.RegularExpressions;

namespace Probe.DbEditor.Security;

public static partial class SqlIdentifier
{
    private const int MaxIdentifierLength = 64;

    public static string Quote(string identifier)
    {
        EnsureValidDatabaseIdentifier(identifier);
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    public static string QuoteQualified(string schemaName, string tableName)
    {
        return $"{Quote(schemaName)}.{Quote(tableName)}";
    }

    public static string ValidateUserDefinedIdentifier(string identifier, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        var trimmed = identifier.Trim();
        if (!UserIdentifierRegex().IsMatch(trimmed))
        {
            throw new InvalidOperationException($"{fieldName} must start with a letter or underscore and contain only letters, numbers, underscores, or dollar signs.");
        }

        return trimmed;
    }

    public static IReadOnlyList<string> ValidateColumnAllowList(
        IEnumerable<string> requestedColumns,
        IEnumerable<string> allowedColumns)
    {
        var allowed = new HashSet<string>(allowedColumns, StringComparer.OrdinalIgnoreCase);
        var validatedColumns = new List<string>();

        foreach (var requestedColumn in requestedColumns)
        {
            var column = requestedColumn.Trim();
            if (!allowed.Contains(column))
            {
                throw new InvalidOperationException($"Column '{column}' does not exist on the selected table.");
            }

            validatedColumns.Add(column);
        }

        if (validatedColumns.Count == 0)
        {
            throw new InvalidOperationException("At least one index column is required.");
        }

        return validatedColumns;
    }

    private static void EnsureValidDatabaseIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new InvalidOperationException("Database identifier cannot be empty.");
        }

        if (identifier.Length > MaxIdentifierLength)
        {
            throw new InvalidOperationException($"Database identifier '{identifier}' exceeds {MaxIdentifierLength} characters.");
        }

        if (identifier.Contains('\0', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Database identifier contains an invalid null character.");
        }
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$]{0,63}$")]
    private static partial Regex UserIdentifierRegex();
}
