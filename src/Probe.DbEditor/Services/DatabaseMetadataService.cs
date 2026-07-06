using System.Data;
using Probe.DbEditor.Models;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Services;

public sealed class DatabaseMetadataService
{
    private readonly DatabaseCommandExecutor _executor;

    public DatabaseMetadataService(DatabaseCommandExecutor executor)
    {
        _executor = executor;
    }

    public async Task<IReadOnlyList<string>> LoadSchemasAsync(CancellationToken cancellationToken = default)
    {
        var table = await _executor.ExecuteTableAsync("SHOW DATABASES", _ => { }, cancellationToken);
        return ReadStringColumn(table, 0);
    }

    public async Task<IReadOnlyList<string>> LoadTablesAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = @schema
            ORDER BY TABLE_NAME
            """;

        var table = await _executor.ExecuteTableAsync(
            sql,
            command => command.Parameters.AddWithValue("@schema", schemaName),
            cancellationToken);

        return ReadStringColumn(table, 0);
    }

    public async Task<IReadOnlyList<string>> LoadColumnsAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """;

        var table = await _executor.ExecuteTableAsync(
            sql,
            command =>
            {
                command.Parameters.AddWithValue("@schema", schemaName);
                command.Parameters.AddWithValue("@table", tableName);
            },
            cancellationToken);

        return ReadStringColumn(table, 0);
    }

    public async Task<IReadOnlyList<string>> LoadPrimaryKeyColumnsAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COLUMN_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @schema
              AND TABLE_NAME = @table
              AND CONSTRAINT_NAME = 'PRIMARY'
            ORDER BY ORDINAL_POSITION
            """;

        var table = await _executor.ExecuteTableAsync(
            sql,
            command =>
            {
                command.Parameters.AddWithValue("@schema", schemaName);
                command.Parameters.AddWithValue("@table", tableName);
            },
            cancellationToken);

        return ReadStringColumn(table, 0);
    }

    public async Task<IReadOnlyList<IndexInfo>> LoadIndexesAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT INDEX_NAME, NON_UNIQUE, SEQ_IN_INDEX, COLUMN_NAME, INDEX_TYPE, COLLATION, CARDINALITY
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY INDEX_NAME, SEQ_IN_INDEX
            """;

        var table = await _executor.ExecuteTableAsync(
            sql,
            command =>
            {
                command.Parameters.AddWithValue("@schema", schemaName);
                command.Parameters.AddWithValue("@table", tableName);
            },
            cancellationToken);

        return table.Rows.Cast<DataRow>().Select(row => new IndexInfo
        {
            IndexName = Convert.ToString(row["INDEX_NAME"]) ?? "",
            IsUnique = Convert.ToInt32(row["NON_UNIQUE"]) == 0,
            Sequence = Convert.ToInt32(row["SEQ_IN_INDEX"]),
            ColumnName = Convert.ToString(row["COLUMN_NAME"]) ?? "",
            IndexType = Convert.ToString(row["INDEX_TYPE"]) ?? "",
            Collation = Convert.ToString(row["COLLATION"]) ?? "",
            Cardinality = row["CARDINALITY"] is DBNull ? null : Convert.ToInt64(row["CARDINALITY"])
        }).ToList();
    }

    public async Task CreateIndexAsync(
        string schemaName,
        string tableName,
        string indexName,
        bool isUnique,
        IReadOnlyList<string> requestedColumns,
        CancellationToken cancellationToken = default)
    {
        var safeIndexName = SqlIdentifier.ValidateUserDefinedIdentifier(indexName, "Index name");
        var allowedColumns = await LoadColumnsAsync(schemaName, tableName, cancellationToken);
        var safeColumns = SqlIdentifier.ValidateColumnAllowList(requestedColumns, allowedColumns);
        var columnList = string.Join(", ", safeColumns.Select(SqlIdentifier.Quote));
        var sql = $"CREATE {(isUnique ? "UNIQUE " : "")}INDEX {SqlIdentifier.Quote(safeIndexName)} ON {SqlIdentifier.QuoteQualified(schemaName, tableName)} ({columnList})";
        await _executor.ExecuteNonQueryAsync(sql, _ => { }, cancellationToken);
    }

    public async Task DropIndexAsync(
        string schemaName,
        string tableName,
        string indexName,
        CancellationToken cancellationToken = default)
    {
        var indexes = await LoadIndexesAsync(schemaName, tableName, cancellationToken);
        if (!indexes.Any(index => string.Equals(index.IndexName, indexName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Index '{indexName}' does not exist on the selected table.");
        }

        if (string.Equals(indexName, "PRIMARY", StringComparison.OrdinalIgnoreCase))
        {
            await _executor.ExecuteNonQueryAsync($"ALTER TABLE {SqlIdentifier.QuoteQualified(schemaName, tableName)} DROP PRIMARY KEY", _ => { }, cancellationToken);
            return;
        }

        var sql = $"DROP INDEX {SqlIdentifier.Quote(indexName)} ON {SqlIdentifier.QuoteQualified(schemaName, tableName)}";
        await _executor.ExecuteNonQueryAsync(sql, _ => { }, cancellationToken);
    }

    public async Task<IReadOnlyList<ForeignKeyEdge>> LoadForeignKeysAsync(
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CONSTRAINT_NAME, TABLE_NAME, COLUMN_NAME, REFERENCED_TABLE_NAME, REFERENCED_COLUMN_NAME
            FROM information_schema.KEY_COLUMN_USAGE
            WHERE TABLE_SCHEMA = @schema AND REFERENCED_TABLE_NAME IS NOT NULL
            ORDER BY TABLE_NAME, CONSTRAINT_NAME, ORDINAL_POSITION
            """;

        var table = await _executor.ExecuteTableAsync(
            sql,
            command => command.Parameters.AddWithValue("@schema", schemaName),
            cancellationToken);

        return table.Rows.Cast<DataRow>().Select(row => new ForeignKeyEdge
        {
            ConstraintName = Convert.ToString(row["CONSTRAINT_NAME"]) ?? "",
            TableName = Convert.ToString(row["TABLE_NAME"]) ?? "",
            ColumnName = Convert.ToString(row["COLUMN_NAME"]) ?? "",
            ReferencedTableName = Convert.ToString(row["REFERENCED_TABLE_NAME"]) ?? "",
            ReferencedColumnName = Convert.ToString(row["REFERENCED_COLUMN_NAME"]) ?? ""
        }).ToList();
    }

    private static IReadOnlyList<string> ReadStringColumn(DataTable table, int columnIndex)
    {
        return table.Rows
            .Cast<DataRow>()
            .Select(row => Convert.ToString(row[columnIndex]) ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }
}
