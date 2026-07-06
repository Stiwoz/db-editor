using System.Text.RegularExpressions;
using MySqlConnector;

namespace Probe.DbEditor.Security;

public static partial class QueryLogSanitizer
{
    private const int MaxStatementLength = 4000;

    public static string SanitizeStatement(string statement)
    {
        var singleLine = WhitespaceRegex().Replace(statement, " ").Trim();
        var redacted = SecretAssignmentRegex().Replace(singleLine, "$1<redacted>");
        return redacted.Length <= MaxStatementLength ? redacted : redacted[..MaxStatementLength] + "...";
    }

    public static string SummarizeParameters(MySqlCommand command)
    {
        var names = command.Parameters
            .Cast<MySqlParameter>()
            .Select(parameter => $"{parameter.ParameterName}=<redacted>");

        return string.Join(", ", names);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"((?:password|passwd|pwd|secret|token|api[_-]?key)\s*=\s*)('[^']*'|""[^""]*""|[^\s,;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SecretAssignmentRegex();
}
