using System.Data;
using Probe.DbEditor.Utilities;

namespace Probe.DbEditor.Models;

public sealed class PendingCellEdit
{
    public PendingCellEdit(
        DataRow row,
        string schemaName,
        string tableName,
        string columnName,
        object? oldValue,
        object? newValue,
        IReadOnlyDictionary<string, object?> keyValues)
    {
        Row = row;
        SchemaName = schemaName;
        TableName = tableName;
        ColumnName = columnName;
        OldValue = oldValue;
        NewValue = newValue;
        KeyValues = keyValues;
        OldValueText = ValueDisplay.Format(oldValue);
        NewValueText = ValueDisplay.Format(newValue);
        KeyDisplay = string.Join(", ", keyValues.Select(pair => $"{pair.Key}={ValueDisplay.Format(pair.Value)}"));
    }

    public DataRow Row { get; }
    public string SchemaName { get; }
    public string TableName { get; }
    public string ColumnName { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
    public IReadOnlyDictionary<string, object?> KeyValues { get; }
    public string OldValueText { get; }
    public string NewValueText { get; }
    public string KeyDisplay { get; }
}
