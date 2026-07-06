using System.ComponentModel;
using System.Data;

namespace Probe.DbEditor.Models;

public sealed class TableDataResult
{
    public TableDataResult(
        string schemaName,
        string tableName,
        DataTable rows,
        IReadOnlyList<string> primaryKeyColumns,
        string selectSql,
        string? orderByColumn,
        ListSortDirection? orderDirection)
    {
        SchemaName = schemaName;
        TableName = tableName;
        Rows = rows;
        PrimaryKeyColumns = primaryKeyColumns;
        SelectSql = selectSql;
        OrderByColumn = orderByColumn;
        OrderDirection = orderDirection;
    }

    public string SchemaName { get; }
    public string TableName { get; }
    public DataTable Rows { get; }
    public IReadOnlyList<string> PrimaryKeyColumns { get; }
    public string SelectSql { get; }
    public string? OrderByColumn { get; }
    public ListSortDirection? OrderDirection { get; }
}
