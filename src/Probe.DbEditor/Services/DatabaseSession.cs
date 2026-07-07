using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Services;

public sealed class DatabaseSession : IAsyncDisposable
{
    private SshTunnel? _tunnel;
    private DatabaseCommandExecutor? _executor;
    private DatabaseMetadataService? _metadata;
    private TableDataService? _tables;

    public DatabaseSession(ConnectionProfile profile)
    {
        Profile = profile;
    }

    public ConnectionProfile Profile { get; }
    public ObservableCollection<QueryLogEntry> QueryLog { get; } = [];

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Profile.Protocol == ConnectionProtocolKind.SshTunnel)
            {
                _tunnel = await SshTunnel.OpenAsync(Profile, cancellationToken);
            }

            var connectionString = DatabaseConnectionStringFactory.Create(Profile, _tunnel?.LocalPort);
            _executor = new DatabaseCommandExecutor(connectionString, QueryLog);
            _metadata = new DatabaseMetadataService(_executor);
            _tables = new TableDataService(_executor, _metadata);

            await _executor.ExecuteScalarAsync("SELECT VERSION()", _ => { }, cancellationToken);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public Task<IReadOnlyList<string>> LoadSchemasAsync(CancellationToken cancellationToken = default)
    {
        return Metadata.LoadSchemasAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> LoadTablesAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        return Metadata.LoadTablesAsync(schemaName, cancellationToken);
    }

    public Task<TableDataResult> LoadTableAsync(
        string schemaName,
        string tableName,
        int limit,
        string? orderByColumn = null,
        ListSortDirection? orderDirection = null,
        CancellationToken cancellationToken = default)
    {
        return Tables.LoadTableAsync(schemaName, tableName, limit, orderByColumn, orderDirection, cancellationToken);
    }

    public IAsyncEnumerable<TableDataChunkResult> StreamTableAsync(
        string schemaName,
        string tableName,
        int limit,
        string? orderByColumn = null,
        ListSortDirection? orderDirection = null,
        CancellationToken cancellationToken = default)
    {
        return Tables.StreamTableAsync(schemaName, tableName, limit, orderByColumn, orderDirection, cancellationToken);
    }

    public Task<IReadOnlyList<string>> LoadColumnsAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return Metadata.LoadColumnsAsync(schemaName, tableName, cancellationToken);
    }

    public Task<IReadOnlyList<ColumnInfo>> LoadColumnDetailsAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return Metadata.LoadColumnDetailsAsync(schemaName, tableName, cancellationToken);
    }

    public Task<IReadOnlyList<IndexInfo>> LoadIndexesAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return Metadata.LoadIndexesAsync(schemaName, tableName, cancellationToken);
    }

    public Task CreateIndexAsync(
        string schemaName,
        string tableName,
        string indexName,
        bool isUnique,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken = default)
    {
        return Metadata.CreateIndexAsync(schemaName, tableName, indexName, isUnique, columns, cancellationToken);
    }

    public Task DropIndexAsync(
        string schemaName,
        string tableName,
        string indexName,
        CancellationToken cancellationToken = default)
    {
        return Metadata.DropIndexAsync(schemaName, tableName, indexName, cancellationToken);
    }

    public Task<int> ApplyCellUpdateAsync(PendingCellEdit edit, CancellationToken cancellationToken = default)
    {
        return Tables.ApplyCellUpdateAsync(edit, cancellationToken);
    }

    public Task<SqlExecutionResult> RunSqlAsync(string sql, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return Task.FromResult(new SqlExecutionResult(new DataTable(), 0));
        }

        return Executor.ExecuteReaderAsync(sql, _ => { }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        _tunnel?.Dispose();
    }

    private DatabaseCommandExecutor Executor =>
        _executor ?? throw new InvalidOperationException("Database session has not been opened.");

    private DatabaseMetadataService Metadata =>
        _metadata ?? throw new InvalidOperationException("Database session has not been opened.");

    private TableDataService Tables =>
        _tables ?? throw new InvalidOperationException("Database session has not been opened.");
}
