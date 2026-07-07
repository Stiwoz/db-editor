using System.ComponentModel;
using System.Data;

namespace Probe.DbEditor.Models;

public sealed class TableDataChunkResult
{
    public TableDataChunkResult(
        string schemaName,
        string tableName,
        DataTable rows,
        IReadOnlyList<string> primaryKeyColumns,
        string selectSql,
        string? orderByColumn,
        ListSortDirection? orderDirection,
        int offset,
        int requestedLimit)
    {
        SchemaName = schemaName;
        TableName = tableName;
        Rows = rows;
        PrimaryKeyColumns = primaryKeyColumns;
        SelectSql = selectSql;
        OrderByColumn = orderByColumn;
        OrderDirection = orderDirection;
        Offset = offset;
        RequestedLimit = requestedLimit;
    }

    public string SchemaName { get; }
    public string TableName { get; }
    public DataTable Rows { get; }
    public IReadOnlyList<string> PrimaryKeyColumns { get; }
    public string SelectSql { get; }
    public string? OrderByColumn { get; }
    public ListSortDirection? OrderDirection { get; }
    public int Offset { get; }
    public int RequestedLimit { get; }
}
