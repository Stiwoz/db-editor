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
        Assert.AreEqual("SELECT * FROM `app`.`users` ORDER BY `name` DESC LIMIT @limit OFFSET @offset", selectCall.Sql);
        Assert.AreEqual(1, selectCall.Parameters["@limit"]);
        Assert.AreEqual(0, selectCall.Parameters["@offset"]);
    }

    [TestMethod]
    public async Task LoadTableAsync_ChunksSelectsUntilLimit()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id"));
        executor.EnqueueTable(UserRows(100, 1));
        executor.EnqueueTable(UserRows(100, 101));
        executor.EnqueueTable(UserRows(50, 201));
        var metadata = new DatabaseMetadataService(executor);
        var service = new TableDataService(executor, metadata);

        var result = await service.LoadTableAsync("app", "users", 250);

        Assert.AreEqual(250, result.Rows.Rows.Count);
        var selectCalls = executor.Calls
            .Where(call => call.Sql.StartsWith("SELECT * FROM", StringComparison.Ordinal))
            .ToList();
        Assert.AreEqual(3, selectCalls.Count);
        Assert.AreEqual(100, selectCalls[0].Parameters["@limit"]);
        Assert.AreEqual(0, selectCalls[0].Parameters["@offset"]);
        Assert.AreEqual(100, selectCalls[1].Parameters["@limit"]);
        Assert.AreEqual(100, selectCalls[1].Parameters["@offset"]);
        Assert.AreEqual(50, selectCalls[2].Parameters["@limit"]);
        Assert.AreEqual(200, selectCalls[2].Parameters["@offset"]);
    }

    [TestMethod]
    public async Task StreamTableAsync_StopsBeforeNextChunkWhenCanceled()
    {
        var executor = new FakeDatabaseCommandExecutor();
        executor.EnqueueTable(DataTableFactory.SingleColumn("COLUMN_NAME", "id"));
        executor.EnqueueTable(UserRows(100, 1));
        var metadata = new DatabaseMetadataService(executor);
        var service = new TableDataService(executor, metadata);
        using var cancellation = new CancellationTokenSource();
        await using var stream = service.StreamTableAsync(
            "app",
            "users",
            250,
            cancellationToken: cancellation.Token).GetAsyncEnumerator();

        Assert.IsTrue(await stream.MoveNextAsync());
        Assert.AreEqual(100, stream.Current.Rows.Rows.Count);

        cancellation.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(async () => await stream.MoveNextAsync().AsTask());

        var selectCalls = executor.Calls
            .Where(call => call.Sql.StartsWith("SELECT * FROM", StringComparison.Ordinal))
            .ToList();
        Assert.AreEqual(1, selectCalls.Count);
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

    private static DataTable UserRows(int count = 1, int startId = 7)
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        for (var index = 0; index < count; index++)
        {
            var id = startId + index;
            table.Rows.Add(id, index == 0 ? "Alice" : $"User {id}");
        }

        return table;
    }

    private static DataRow NewRow()
    {
        return UserRows().Rows[0];
    }
}
