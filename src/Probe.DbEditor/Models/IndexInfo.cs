namespace Probe.DbEditor.Models;

public sealed class IndexInfo
{
    public string IndexName { get; init; } = "";
    public bool IsUnique { get; init; }
    public int Sequence { get; init; }
    public string ColumnName { get; init; } = "";
    public string IndexType { get; init; } = "";
    public string Collation { get; init; } = "";
    public long? Cardinality { get; init; }
}
