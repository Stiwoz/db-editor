using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using MySqlConnector;
using Probe.DbEditor.Models;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Services;

public sealed class DatabaseCommandExecutor : IDatabaseCommandExecutor
{
    private readonly string _connectionString;
    private readonly ObservableCollection<QueryLogEntry> _queryLog;
    private readonly SynchronizationContext? _queryLogContext;

    public DatabaseCommandExecutor(string connectionString, ObservableCollection<QueryLogEntry> queryLog)
    {
        _connectionString = connectionString;
        _queryLog = queryLog;
        _queryLogContext = SynchronizationContext.Current;
    }

    public async Task<object?> ExecuteScalarAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        return await ExecuteLoggedAsync(sql, configure, cancellationToken, async command =>
        {
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (value, -1);
        }).ConfigureAwait(false);
    }

    public async Task<DataTable> ExecuteTableAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteReaderAsync(sql, configure, cancellationToken).ConfigureAwait(false);
        return result.Rows;
    }

    public async Task<SqlExecutionResult> ExecuteReaderAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        return await ExecuteLoggedAsync(sql, configure, cancellationToken, async command =>
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var table = await LoadReaderAsync(reader, cancellationToken).ConfigureAwait(false);
            return (new SqlExecutionResult(table, reader.RecordsAffected), reader.RecordsAffected);
        }).ConfigureAwait(false);
    }

    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        return await ExecuteLoggedAsync(sql, configure, cancellationToken, async command =>
        {
            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return (rowsAffected, rowsAffected);
        }).ConfigureAwait(false);
    }

    private async Task<T> ExecuteLoggedAsync<T>(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken,
        Func<MySqlCommand, Task<(T Result, int RowsAffected)>> execute)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
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
            AddQueryLogEntry(entry);
        }
    }

    private void AddQueryLogEntry(QueryLogEntry entry)
    {
        if (_queryLogContext is null || SynchronizationContext.Current == _queryLogContext)
        {
            _queryLog.Add(entry);
            return;
        }

        _queryLogContext.Post(_ => _queryLog.Add(entry), null);
    }

    private static async Task<DataTable> LoadReaderAsync(
        MySqlDataReader reader,
        CancellationToken cancellationToken)
    {
        var table = new DataTable();
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            table.Columns.Add(
                CreateUniqueColumnName(table.Columns, reader.GetName(ordinal), ordinal),
                GetFieldType(reader, ordinal));
        }

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            table.Rows.Add(values);
        }

        table.AcceptChanges();
        return table;
    }

    private static string CreateUniqueColumnName(
        DataColumnCollection columns,
        string columnName,
        int ordinal)
    {
        var baseName = string.IsNullOrWhiteSpace(columnName)
            ? $"Column{ordinal + 1}"
            : columnName;
        var candidate = baseName;
        var suffix = 1;

        while (columns.Cast<DataColumn>().Any(column =>
                   string.Equals(column.ColumnName, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static Type GetFieldType(MySqlDataReader reader, int ordinal)
    {
        try
        {
            return reader.GetFieldType(ordinal) ?? typeof(object);
        }
        catch (InvalidOperationException)
        {
            return typeof(object);
        }
        catch (NotSupportedException)
        {
            return typeof(object);
        }
    }
}
