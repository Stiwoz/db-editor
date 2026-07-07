using System.ComponentModel;
using System.Globalization;
using System.Data;
using System.Runtime.CompilerServices;
using Probe.DbEditor.Models;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Services;

public sealed class TableDataService
{
    public const int TableLoadChunkSize = 100;

    private readonly IDatabaseCommandExecutor _executor;
    private readonly DatabaseMetadataService _metadata;

    public TableDataService(IDatabaseCommandExecutor executor, DatabaseMetadataService metadata)
    {
        _executor = executor;
        _metadata = metadata;
    }

    public async Task<TableDataResult> LoadTableAsync(
        string schemaName,
        string tableName,
        int limit,
        string? orderByColumn = null,
        ListSortDirection? orderDirection = null,
        CancellationToken cancellationToken = default)
    {
        DataTable? rows = null;
        TableDataChunkResult? lastChunk = null;
        await foreach (var chunk in StreamTableAsync(
                           schemaName,
                           tableName,
                           limit,
                           orderByColumn,
                           orderDirection,
                           cancellationToken))
        {
            lastChunk = chunk;
            rows ??= CreateResultTable(tableName, chunk.Rows);
            AppendRows(rows, chunk.Rows);
        }

        if (lastChunk is null || rows is null)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        return new TableDataResult(
            lastChunk.SchemaName,
            lastChunk.TableName,
            rows,
            lastChunk.PrimaryKeyColumns,
            lastChunk.SelectSql,
            lastChunk.OrderByColumn,
            lastChunk.OrderDirection);
    }

    public async IAsyncEnumerable<TableDataChunkResult> StreamTableAsync(
        string schemaName,
        string tableName,
        int limit,
        string? orderByColumn = null,
        ListSortDirection? orderDirection = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var validatedOrderByColumn = await ValidateOrderColumnAsync(
            schemaName,
            tableName,
            orderByColumn,
            cancellationToken).ConfigureAwait(false);
        var primaryKeys = await _metadata.LoadPrimaryKeyColumnsAsync(
            schemaName,
            tableName,
            cancellationToken).ConfigureAwait(false);
        var limitValue = Math.Max(1, limit);
        var displaySql = BuildSelectSql(
            schemaName,
            tableName,
            validatedOrderByColumn,
            orderDirection,
            limitValue.ToString(CultureInfo.InvariantCulture));

        for (var offset = 0; offset < limitValue; offset += TableLoadChunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkLimit = Math.Min(TableLoadChunkSize, limitValue - offset);
            var sql = BuildSelectSql(
                schemaName,
                tableName,
                validatedOrderByColumn,
                orderDirection,
                "@limit",
                "@offset");
            var chunk = await _executor.ExecuteTableAsync(
                sql,
                command =>
                {
                    command.Parameters.AddWithValue("@limit", chunkLimit);
                    command.Parameters.AddWithValue("@offset", offset);
                },
                cancellationToken).ConfigureAwait(false);

            chunk.TableName = tableName;
            chunk.AcceptChanges();

            if (chunk.Rows.Count > 0 || offset == 0)
            {
                yield return new TableDataChunkResult(
                    schemaName,
                    tableName,
                    chunk,
                    primaryKeys,
                    displaySql,
                    validatedOrderByColumn,
                    orderDirection,
                    offset,
                    chunkLimit);
            }

            if (chunk.Rows.Count < chunkLimit)
            {
                break;
            }
        }
    }

    private static DataTable CreateResultTable(string tableName, DataTable chunk)
    {
        var rows = chunk.Clone();
        rows.TableName = tableName;
        return rows;
    }

    private static void AppendRows(DataTable rows, DataTable chunk)
    {
        foreach (DataRow row in chunk.Rows)
        {
            var appended = rows.NewRow();
            appended.ItemArray = row.ItemArray;
            rows.Rows.Add(appended);
            appended.AcceptChanges();
        }
    }

    public async Task<int> ApplyCellUpdateAsync(PendingCellEdit edit, CancellationToken cancellationToken = default)
    {
        if (edit.KeyValues.Count == 0)
        {
            throw new InvalidOperationException("Cannot update a table without a primary key.");
        }

        var columns = await _metadata.LoadColumnsAsync(edit.SchemaName, edit.TableName, cancellationToken);
        if (!columns.Contains(edit.ColumnName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Column '{edit.ColumnName}' does not exist on the selected table.");
        }

        var keyColumns = await _metadata.LoadPrimaryKeyColumnsAsync(edit.SchemaName, edit.TableName, cancellationToken);
        foreach (var key in edit.KeyValues.Keys)
        {
            if (!keyColumns.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Primary key column '{key}' is not valid for the selected table.");
            }
        }

        var whereClause = string.Join(" AND ", edit.KeyValues.Keys.Select(key => $"{SqlIdentifier.Quote(key)} <=> @{key}"));
        var sql = $"UPDATE {SqlIdentifier.QuoteQualified(edit.SchemaName, edit.TableName)} SET {SqlIdentifier.Quote(edit.ColumnName)} = @new_value WHERE {whereClause} LIMIT 1";

        return await _executor.ExecuteNonQueryAsync(
            sql,
            command =>
            {
                command.Parameters.AddWithValue("@new_value", edit.NewValue ?? DBNull.Value);
                foreach (var pair in edit.KeyValues)
                {
                    command.Parameters.AddWithValue($"@{pair.Key}", pair.Value ?? DBNull.Value);
                }
            },
            cancellationToken);
    }

    private async Task<string?> ValidateOrderColumnAsync(
        string schemaName,
        string tableName,
        string? orderByColumn,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderByColumn))
        {
            return null;
        }

        var columns = await _metadata.LoadColumnsAsync(schemaName, tableName, cancellationToken).ConfigureAwait(false);
        return columns.FirstOrDefault(column => string.Equals(column, orderByColumn, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Column '{orderByColumn}' does not exist on the selected table.");
    }

    private static string BuildSelectSql(
        string schemaName,
        string tableName,
        string? orderByColumn,
        ListSortDirection? orderDirection,
        string limitExpression,
        string? offsetExpression = null)
    {
        var sql = $"SELECT * FROM {SqlIdentifier.QuoteQualified(schemaName, tableName)}";
        if (!string.IsNullOrWhiteSpace(orderByColumn))
        {
            var direction = orderDirection == ListSortDirection.Descending ? "DESC" : "ASC";
            sql += $" ORDER BY {SqlIdentifier.Quote(orderByColumn)} {direction}";
        }

        sql += $" LIMIT {limitExpression}";
        if (!string.IsNullOrWhiteSpace(offsetExpression))
        {
            sql += $" OFFSET {offsetExpression}";
        }

        return sql;
    }
}
