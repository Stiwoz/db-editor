namespace Probe.DbEditor.Models;

public sealed class ColumnInfo
{
    public int Position { get; init; }
    public string Name { get; init; } = "";
    public string ColumnType { get; init; } = "";
    public string DataType { get; init; } = "";
    public string IsNullable { get; init; } = "";
    public string DefaultValue { get; init; } = "";
    public long? CharacterMaximumLength { get; init; }
    public long? NumericPrecision { get; init; }
    public long? NumericScale { get; init; }
    public long? DateTimePrecision { get; init; }
    public string CharacterSet { get; init; } = "";
    public string Collation { get; init; } = "";
    public string Key { get; init; } = "";
    public string Extra { get; init; } = "";
    public string Comment { get; init; } = "";
    public string GenerationExpression { get; init; } = "";
}
