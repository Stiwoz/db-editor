using System.Data;

namespace Probe.DbEditor.Models;

public sealed class SqlExecutionResult
{
    public SqlExecutionResult(DataTable rows, int rowsAffected)
    {
        Rows = rows;
        RowsAffected = rowsAffected;
    }

    public DataTable Rows { get; }
    public int RowsAffected { get; }
}
