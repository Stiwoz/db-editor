using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using MySqlConnector;
using Probe.DbEditor.Models;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Services;

public sealed class DatabaseCommandExecutor
{
    private readonly string _connectionString;
    private readonly ObservableCollection<QueryLogEntry> _queryLog;

    public DatabaseCommandExecutor(string connectionString, ObservableCollection<QueryLogEntry> queryLog)
    {
        _connectionString = connectionString;
        _queryLog = queryLog;
    }

    public async Task<object?> ExecuteScalarAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        return await ExecuteLoggedAsync(sql, configure, async command =>
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return (value, -1);
        });
    }

    public async Task<DataTable> ExecuteTableAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteReaderAsync(sql, configure, cancellationToken);
        return result.Rows;
    }

    public async Task<SqlExecutionResult> ExecuteReaderAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        return await ExecuteLoggedAsync(sql, configure, async command =>
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var table = new DataTable();
            table.Load(reader);
            return (new SqlExecutionResult(table, reader.RecordsAffected), reader.RecordsAffected);
        });
    }

    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        return await ExecuteLoggedAsync(sql, configure, async command =>
        {
            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            return (rowsAffected, rowsAffected);
        });
    }

    private async Task<T> ExecuteLoggedAsync<T>(
        string sql,
        Action<MySqlCommand> configure,
        Func<MySqlCommand, Task<(T Result, int RowsAffected)>> execute)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        configure(command);

        var entry = new QueryLogEntry
        {
            StartedAt = DateTimeOffset.Now,
            Statement = QueryLogSanitizer.SanitizeStatement(sql),
            Parameters = QueryLogSanitizer.SummarizeParameters(command)
        };

        try
        {
            var result = await execute(command);
            entry.Success = true;
            entry.RowsAffected = result.RowsAffected;
            return result.Result;
        }
        catch (Exception ex)
        {
            entry.Success = false;
            entry.Error = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            entry.DurationMs = stopwatch.ElapsedMilliseconds;
            _queryLog.Add(entry);
        }
    }
}
