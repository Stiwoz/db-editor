using System.Data;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Tests.Models;

[TestClass]
public sealed class PendingCellEditTests
{
    [TestMethod]
    public void Constructor_FormatsValuesForReviewGrid()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        var row = table.Rows.Add(1);

        var edit = new PendingCellEdit(
            row,
            "app",
            "files",
            "content",
            DBNull.Value,
            new byte[] { 1, 2, 3 },
            new Dictionary<string, object?> { ["id"] = 1, ["tenant"] = null });

        Assert.AreEqual("NULL", edit.OldValueText);
        Assert.AreEqual("<binary 3 bytes>", edit.NewValueText);
        Assert.AreEqual("id=1, tenant=NULL", edit.KeyDisplay);
    }
}
