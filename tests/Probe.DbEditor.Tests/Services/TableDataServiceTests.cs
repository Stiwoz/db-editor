using System.ComponentModel;
using System.Data;
using Probe.DbEditor.Models;
using Probe.DbEditor.Services;
using Probe.DbEditor.Tests.TestDoubles;

namespace Probe.DbEditor.Tests.Services;

[TestClass]
public sealed class TableDataServiceTests
{
    [TestMethod]
    public async Task LoadTableAsync_UsesParameterizedLimitAndAllowListedOrderColumn()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id", "name"));
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id"));
        executor.EnqueueTable(UserRows());
        var metadata = new DatabaseMetadataService(executor);
        var service = new TableDataService(executor, metadata);

        var result = await service.LoadTableAsync(
            "app",
            "users",
            0,
            "name",
            ListSortDirection.Descending);

        Assert.AreEqual("SELECT * FROM `app`.`users` ORDER BY `name` DESC LIMIT 1", result.SelectSql);
        CollectionAssert.AreEqual(new[] { "id" }, result.PrimaryKeyColumns.ToArray());
        Assert.AreEqual(1, result.Rows.Rows.Count);
        Assert.AreEqual(DataRowState.Unchanged, result.Rows.Rows[0].RowState);

        var selectCall = executor.Calls.Last();
        Assert.AreEqual("SELECT * FROM `app`.`users` ORDER BY `name` DESC LIMIT @limit", selectCall.Sql);
        Assert.AreEqual(1, selectCall.Parameters["@limit"]);
    }

    [TestMethod]
    public async Task LoadTableAsync_RejectsUnknownOrderColumnBeforeRunningSelect()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id", "name"));
        var metadata = new DatabaseMetadataService(executor);
        var service = new TableDataService(executor, metadata);

        await TestAssert.ThrowsAsync<InvalidOperationException>(
            () => service.LoadTableAsync("app", "users", 500, "name;DROP", ListSortDirection.Ascending));

        Assert.AreEqual(1, executor.Calls.Count);
    }

    [TestMethod]
    public async Task ApplyCellUpdateAsync_UsesColumnAndPrimaryKeyAllowLists()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id", "name"));
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id"));
        executor.EnqueueNonQueryResult(1);
        var metadata = new DatabaseMetadataService(executor);
        var service = new TableDataService(executor, metadata);
        var edit = new PendingCellEdit(
            NewRow(),
            "app",
            "users",
            "name",
            "old",
            "new",
            new Dictionary<string, object?> { ["id"] = 7 });

        var affected = await service.ApplyCellUpdateAsync(edit);

        Assert.AreEqual(1, affected);
        var update = executor.Calls.Last();
        Assert.AreEqual("UPDATE `app`.`users` SET `name` = @new_value WHERE `id` <=> @id LIMIT 1", update.Sql);
        Assert.AreEqual("new", update.Parameters["@new_value"]);
        Assert.AreEqual(7, update.Parameters["@id"]);
    }

    [TestMethod]
    public async Task ApplyCellUpdateAsync_RejectsTablesWithoutPrimaryKeys()
    {
        var executor = new FakeDatabaseCommandExecutor();
        var metadata = new DatabaseMetadataService(executor);
        var service = new TableDataService(executor, metadata);
        var edit = new PendingCellEdit(NewRow(), "app", "users", "name", "old", "new", new Dictionary<string, object?>());

        await TestAssert.ThrowsAsync<InvalidOperationException>(() => service.ApplyCellUpdateAsync(edit));

        Assert.AreEqual(0, executor.Calls.Count);
    }

    [TestMethod]
    public async Task ApplyCellUpdateAsync_RejectsUnknownUpdateColumn()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id", "name"));
        var metadata = new DatabaseMetadataService(executor);
        var service = new TableDataService(executor, metadata);
        var edit = new PendingCellEdit(
            NewRow(),
            "app",
            "users",
            "name;DROP",
            "old",
            "new",
            new Dictionary<string, object?> { ["id"] = 7 });

        await TestAssert.ThrowsAsync<InvalidOperationException>(() => service.ApplyCellUpdateAsync(edit));

        Assert.IsFalse(executor.Calls.Any(call => call.Operation == "NonQuery"));
    }

    [TestMethod]
    public async Task ApplyCellUpdateAsync_RejectsUnknownPrimaryKeyColumn()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id", "name"));
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id"));
        var metadata = new DatabaseMetadataService(executor);
        var service = new TableDataService(executor, metadata);
        var edit = new PendingCellEdit(
            NewRow(),
            "app",
            "users",
            "name",
            "old",
            "new",
            new Dictionary<string, object?> { ["id;DROP"] = 7 });

        await TestAssert.ThrowsAsync<InvalidOperationException>(() => service.ApplyCellUpdateAsync(edit));

        Assert.IsFalse(executor.Calls.Any(call => call.Operation == "NonQuery"));
    }

    private static DataTable UserRows()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(7, "Alice");
        return table;
    }

    private static DataRow NewRow()
    {
        return UserRows().Rows[0];
    }
}
