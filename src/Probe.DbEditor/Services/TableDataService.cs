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
        CancellationToken cancellationToken = default)
    {
        var primaryKeys = await _metadata.LoadPrimaryKeyColumnsAsync(schemaName, tableName, cancellationToken);
        var sql = $"SELECT * FROM {SqlIdentifier.QuoteQualified(schemaName, tableName)} LIMIT @limit";
        var rows = await _executor.ExecuteTableAsync(
            sql,
            command => command.Parameters.AddWithValue("@limit", Math.Max(1, limit)),
            cancellationToken);

        rows.TableName = tableName;
        rows.AcceptChanges();
        return new TableDataResult(schemaName, tableName, rows, primaryKeys);
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
}
