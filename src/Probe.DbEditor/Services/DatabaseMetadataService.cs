using System.Data;
using Probe.DbEditor.Models;
using Probe.DbEditor.Security;

namespace Probe.DbEditor.Services;

public sealed class DatabaseMetadataService
{
    private readonly IDatabaseCommandExecutor _executor;

    public DatabaseMetadataService(IDatabaseCommandExecutor executor)
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

    public async Task<IReadOnlyList<ColumnInfo>> LoadColumnDetailsAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ORDINAL_POSITION,
                   COLUMN_NAME,
                   COLUMN_TYPE,
                   DATA_TYPE,
                   IS_NULLABLE,
                   COLUMN_DEFAULT,
                   CHARACTER_MAXIMUM_LENGTH,
                   NUMERIC_PRECISION,
                   NUMERIC_SCALE,
                   DATETIME_PRECISION,
                   CHARACTER_SET_NAME,
                   COLLATION_NAME,
                   COLUMN_KEY,
                   EXTRA,
                   COLUMN_COMMENT,
                   GENERATION_EXPRESSION
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

        return table.Rows.Cast<DataRow>().Select(row => new ColumnInfo
        {
            Position = Convert.ToInt32(row["ORDINAL_POSITION"]),
            Name = ReadString(row, "COLUMN_NAME"),
            ColumnType = ReadString(row, "COLUMN_TYPE"),
            DataType = ReadString(row, "DATA_TYPE"),
            IsNullable = ReadString(row, "IS_NULLABLE"),
            DefaultValue = ReadNullableLiteral(row, "COLUMN_DEFAULT"),
            CharacterMaximumLength = ReadNullableInt64(row, "CHARACTER_MAXIMUM_LENGTH"),
            NumericPrecision = ReadNullableInt64(row, "NUMERIC_PRECISION"),
            NumericScale = ReadNullableInt64(row, "NUMERIC_SCALE"),
            DateTimePrecision = ReadNullableInt64(row, "DATETIME_PRECISION"),
            CharacterSet = ReadString(row, "CHARACTER_SET_NAME"),
            Collation = ReadString(row, "COLLATION_NAME"),
            Key = ReadString(row, "COLUMN_KEY"),
            Extra = ReadString(row, "EXTRA"),
            Comment = ReadString(row, "COLUMN_COMMENT"),
            GenerationExpression = ReadString(row, "GENERATION_EXPRESSION")
        }).ToList();
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

    private static IReadOnlyList<string> ReadStringColumn(DataTable table, int columnIndex)
    {
        return table.Rows
            .Cast<DataRow>()
            .Select(row => Convert.ToString(row[columnIndex]) ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string ReadString(DataRow row, string columnName)
    {
        return row[columnName] is DBNull ? "" : Convert.ToString(row[columnName]) ?? "";
    }

    private static string ReadNullableLiteral(DataRow row, string columnName)
    {
        return row[columnName] is DBNull ? "NULL" : Convert.ToString(row[columnName]) ?? "";
    }

    private static long? ReadNullableInt64(DataRow row, string columnName)
    {
        return row[columnName] is DBNull ? null : Convert.ToInt64(row[columnName]);
    }
}
