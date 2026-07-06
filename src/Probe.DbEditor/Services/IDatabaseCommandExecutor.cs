using System.Data;
using MySqlConnector;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Services;

public interface IDatabaseCommandExecutor
{
    Task<object?> ExecuteScalarAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken);

    Task<DataTable> ExecuteTableAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken);

    Task<SqlExecutionResult> ExecuteReaderAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken);

    Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken);
}
