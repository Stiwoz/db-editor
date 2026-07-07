using System.Collections.ObjectModel;
using System.Data;
using MySqlConnector;
using Probe.DbEditor.Models;
using Probe.DbEditor.Services;

namespace Probe.DbEditor.Tests.TestDoubles;

internal sealed class FakeDatabaseCommandExecutor : IDatabaseCommandExecutor
{
    private readonly Queue<DataTable> _tables = [];
    private readonly Queue<int> _nonQueryResults = [];
    private readonly Queue<object?> _scalarResults = [];
    private readonly Queue<SqlExecutionResult> _readerResults = [];

    public Collection<CommandCall> Calls { get; } = [];

    public void EnqueueTable(DataTable table)
    {
        _tables.Enqueue(table);
    }

    public void EnqueueNonQueryResult(int rowsAffected)
    {
        _nonQueryResults.Enqueue(rowsAffected);
    }

    public void EnqueueScalarResult(object? result)
    {
        _scalarResults.Enqueue(result);
    }

    public void EnqueueReaderResult(SqlExecutionResult result)
    {
        _readerResults.Enqueue(result);
    }

    public Task<object?> ExecuteScalarAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var command = Capture("Scalar", sql, configure);
        return Task.FromResult(_scalarResults.Count == 0 ? null : _scalarResults.Dequeue());
    }

    public Task<DataTable> ExecuteTableAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var command = Capture("Table", sql, configure);
        return Task.FromResult(_tables.Count == 0 ? new DataTable() : _tables.Dequeue());
    }

    public Task<SqlExecutionResult> ExecuteReaderAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var command = Capture("Reader", sql, configure);
        return Task.FromResult(_readerResults.Count == 0
            ? new SqlExecutionResult(new DataTable(), 0)
            : _readerResults.Dequeue());
    }

    public Task<int> ExecuteNonQueryAsync(
        string sql,
        Action<MySqlCommand> configure,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var command = Capture("NonQuery", sql, configure);
        return Task.FromResult(_nonQueryResults.Count == 0 ? 1 : _nonQueryResults.Dequeue());
    }

    private MySqlCommand Capture(
        string operation,
        string sql,
        Action<MySqlCommand> configure)
    {
        var command = new MySqlCommand(sql);
        configure(command);

        Calls.Add(new CommandCall(
            operation,
            sql,
            command.Parameters
                .Cast<MySqlParameter>()
                .ToDictionary(parameter => parameter.ParameterName, parameter => parameter.Value)));

        return command;
    }
}
