using Probe.DbEditor.Services;
using Probe.DbEditor.Tests.TestDoubles;

namespace Probe.DbEditor.Tests.Services;

[TestClass]
public sealed class DatabaseMetadataServiceTests
{
    [TestMethod]
    public async Task LoadSchemasAsync_ReturnsNonBlankDatabaseNames()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("Database", "app", "", null, "mysql"));
        var service = new DatabaseMetadataService(executor);

        var schemas = await service.LoadSchemasAsync();

        CollectionAssert.AreEqual(new[] { "app", "mysql" }, schemas.ToArray());
        Assert.AreEqual("SHOW DATABASES", executor.Calls[0].Sql);
    }

    [TestMethod]
    public async Task LoadTablesAsync_UsesParameterizedSchemaFilter()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("TABLE_NAME", "users", "orders"));
        var service = new DatabaseMetadataService(executor);

        var tables = await service.LoadTablesAsync("app");

        CollectionAssert.AreEqual(new[] { "users", "orders" }, tables.ToArray());
        StringAssert.Contains(executor.Calls[0].Sql, "WHERE TABLE_SCHEMA = @schema");
        Assert.AreEqual("app", executor.Calls[0].Parameters["@schema"]);
    }

    [TestMethod]
    public async Task LoadColumnDetailsAsync_MapsColumnMetadataAndNulls()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.ColumnDetails());
        var service = new DatabaseMetadataService(executor);

        var column = AssertSingle(await service.LoadColumnDetailsAsync("app", "users"));

        Assert.AreEqual(1, column.Position);
        Assert.AreEqual("email_address", column.Name);
        Assert.AreEqual("varchar(255)", column.ColumnType);
        Assert.AreEqual("varchar", column.DataType);
        Assert.AreEqual("NO", column.IsNullable);
        Assert.AreEqual("NULL", column.DefaultValue);
        Assert.AreEqual(255L, column.CharacterMaximumLength);
        Assert.IsNull(column.NumericPrecision);
        Assert.AreEqual("utf8mb4_0900_ai_ci", column.Collation);
        Assert.AreEqual("UNI", column.Key);
        Assert.AreEqual("contact email", column.Comment);
        Assert.AreEqual("app", executor.Calls[0].Parameters["@schema"]);
        Assert.AreEqual("users", executor.Calls[0].Parameters["@table"]);
    }

    [TestMethod]
    public async Task CreateIndexAsync_AllowListsColumnsAndQuotesIdentifiers()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id", "email"));
        var service = new DatabaseMetadataService(executor);

        await service.CreateIndexAsync("app", "users", "idx_email", true, ["email"]);

        var nonQuery = executor.Calls.Single(call => call.Operation == "NonQuery");
        Assert.AreEqual("CREATE UNIQUE INDEX `idx_email` ON `app`.`users` (`email`)", nonQuery.Sql);
    }

    [TestMethod]
    public async Task CreateIndexAsync_RejectsUnknownColumnsBeforeExecutingDynamicSql()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id", "email"));
        var service = new DatabaseMetadataService(executor);

        await TestAssert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateIndexAsync("app", "users", "idx_bad", false, ["email; DROP TABLE users"]));

        Assert.IsFalse(executor.Calls.Any(call => call.Operation == "NonQuery"));
    }

    [TestMethod]
    public async Task DropIndexAsync_UsesPrimaryKeyAlterSyntaxForPrimaryIndex()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.Indexes(("PRIMARY", 0, 1, "id")));
        var service = new DatabaseMetadataService(executor);

        await service.DropIndexAsync("app", "users", "PRIMARY");

        var nonQuery = executor.Calls.Single(call => call.Operation == "NonQuery");
        Assert.AreEqual("ALTER TABLE `app`.`users` DROP PRIMARY KEY", nonQuery.Sql);
    }

    [TestMethod]
    public async Task DropIndexAsync_RejectsUnknownIndexBeforeExecutingSql()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.Indexes(("idx_email", 1, 1, "email")));
        var service = new DatabaseMetadataService(executor);

        await TestAssert.ThrowsAsync<InvalidOperationException>(
            () => service.DropIndexAsync("app", "users", "idx_missing"));

        Assert.IsFalse(executor.Calls.Any(call => call.Operation == "NonQuery"));
    }

    private static T AssertSingle<T>(IReadOnlyList<T> values)
    {
        Assert.AreEqual(1, values.Count);
        return values[0];
    }
}
