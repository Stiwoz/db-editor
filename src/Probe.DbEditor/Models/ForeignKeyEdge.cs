namespace Probe.DbEditor.Models;

public sealed class ForeignKeyEdge
{
    public string ConstraintName { get; init; } = "";
    public string TableName { get; init; } = "";
    public string ColumnName { get; init; } = "";
    public string ReferencedTableName { get; init; } = "";
    public string ReferencedColumnName { get; init; } = "";
}
