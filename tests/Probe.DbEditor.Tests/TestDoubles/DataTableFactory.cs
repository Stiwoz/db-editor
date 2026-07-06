using System.Data;

namespace Probe.DbEditor.Tests.TestDoubles;

internal static class DataTableFactory
{
    public static DataTable SingleColumn(string columnName, params object?[] values)
    {
        var table = new DataTable();
        table.Columns.Add(columnName, typeof(object));
        foreach (var value in values)
        {
            table.Rows.Add(value ?? DBNull.Value);
        }

        return table;
    }

    public static DataTable Rows(params (string ColumnName, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (columnName, type) in columns)
        {
            table.Columns.Add(columnName, type);
        }

        return table;
    }

    public static DataTable Indexes(params (string Name, int NonUnique, int Sequence, string ColumnName)[] indexes)
    {
        var table = Rows(
            ("INDEX_NAME", typeof(string)),
            ("NON_UNIQUE", typeof(int)),
            ("SEQ_IN_INDEX", typeof(int)),
            ("COLUMN_NAME", typeof(string)),
            ("INDEX_TYPE", typeof(string)),
            ("COLLATION", typeof(string)),
            ("CARDINALITY", typeof(object)));

        foreach (var index in indexes)
        {
            table.Rows.Add(index.Name, index.NonUnique, index.Sequence, index.ColumnName, "BTREE", "A", 42L);
        }

        return table;
    }

    public static DataTable ColumnDetails()
    {
        var table = Rows(
            ("ORDINAL_POSITION", typeof(int)),
            ("COLUMN_NAME", typeof(string)),
            ("COLUMN_TYPE", typeof(string)),
            ("DATA_TYPE", typeof(string)),
            ("IS_NULLABLE", typeof(string)),
            ("COLUMN_DEFAULT", typeof(object)),
            ("CHARACTER_MAXIMUM_LENGTH", typeof(object)),
            ("NUMERIC_PRECISION", typeof(object)),
            ("NUMERIC_SCALE", typeof(object)),
            ("DATETIME_PRECISION", typeof(object)),
            ("CHARACTER_SET_NAME", typeof(object)),
            ("COLLATION_NAME", typeof(object)),
            ("COLUMN_KEY", typeof(string)),
            ("EXTRA", typeof(string)),
            ("COLUMN_COMMENT", typeof(string)),
            ("GENERATION_EXPRESSION", typeof(string)));

        table.Rows.Add(
            1,
            "email_address",
            "varchar(255)",
            "varchar",
            "NO",
            DBNull.Value,
            255L,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            "utf8mb4",
            "utf8mb4_0900_ai_ci",
            "UNI",
            "",
            "contact email",
            "");

        return table;
    }
}
