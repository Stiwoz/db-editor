using System.ComponentModel;
using System.Globalization;
using System.Data;
using Probe.DbEditor.Models;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Services;

public sealed class TableDataService
{
    private readonly DatabaseCommandExecutor _executor;
    private readonly DatabaseMetadataService _metadata;

    public TableDataService(DatabaseCommandExecutor executor, DatabaseMetadataService metadata)
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
        var validatedOrderByColumn = await ValidateOrderColumnAsync(
            schemaName,
            tableName,
            orderByColumn,
            cancellationToken);
        var primaryKeys = await _metadata.LoadPrimaryKeyColumnsAsync(schemaName, tableName, cancellationToken);
        var limitValue = Math.Max(1, limit);
        var sql = BuildSelectSql(schemaName, tableName, validatedOrderByColumn, orderDirection, "@limit");
        var displaySql = BuildSelectSql(
            schemaName,
            tableName,
            validatedOrderByColumn,
            orderDirection,
            limitValue.ToString(CultureInfo.InvariantCulture));
        var rows = await _executor.ExecuteTableAsync(
            sql,
            command => command.Parameters.AddWithValue("@limit", limitValue),
            cancellationToken);

        rows.TableName = tableName;
        rows.AcceptChanges();
        return new TableDataResult(
            schemaName,
            tableName,
            rows,
            primaryKeys,
            displaySql,
            validatedOrderByColumn,
            orderDirection);
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

        var columns = await _metadata.LoadColumnsAsync(schemaName, tableName, cancellationToken);
        return columns.FirstOrDefault(column => string.Equals(column, orderByColumn, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Column '{orderByColumn}' does not exist on the selected table.");
    }

    private static string BuildSelectSql(
        string schemaName,
        string tableName,
        string? orderByColumn,
        ListSortDirection? orderDirection,
        string limitExpression)
    {
        var sql = $"SELECT * FROM {SqlIdentifier.QuoteQualified(schemaName, tableName)}";
        if (!string.IsNullOrWhiteSpace(orderByColumn))
        {
            var direction = orderDirection == ListSortDirection.Descending ? "DESC" : "ASC";
            sql += $" ORDER BY {SqlIdentifier.Quote(orderByColumn)} {direction}";
        }

        return $"{sql} LIMIT {limitExpression}";
    }
}
