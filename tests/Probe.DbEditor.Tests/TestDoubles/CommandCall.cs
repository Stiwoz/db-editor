namespace Probe.DbEditor.Tests.TestDoubles;

internal sealed record CommandCall(
    string Operation,
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters);
