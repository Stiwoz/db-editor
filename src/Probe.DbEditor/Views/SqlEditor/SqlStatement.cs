namespace Probe.DbEditor.Views.SqlEditor;

public sealed record SqlStatement(
    string Text,
    int StartOffset,
    int EndOffset,
    SqlStatementKind Kind);
