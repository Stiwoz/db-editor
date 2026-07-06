using System.Data;

namespace Probe.DbEditor.Models;

public sealed class TableDataResult
{
    public TableDataResult(string schemaName, string tableName, DataTable rows, IReadOnlyList<string> primaryKeyColumns)
    {
        SchemaName = schemaName;
        TableName = tableName;
        Rows = rows;
        PrimaryKeyColumns = primaryKeyColumns;
    }

    public string SchemaName { get; }
    public string TableName { get; }
    public DataTable Rows { get; }
    public IReadOnlyList<string> PrimaryKeyColumns { get; }
}
