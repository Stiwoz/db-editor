using System.Globalization;
using Probe.DbEditor.Utilities;

namespace Probe.DbEditor.Tests.Utilities;

[TestClass]
public sealed class ValueDisplayTests
{
    [TestMethod]
    public void Format_HandlesNullsBinaryAndInvariantFormatting()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");

            Assert.AreEqual("NULL", ValueDisplay.Format(null));
            Assert.AreEqual("NULL", ValueDisplay.Format(DBNull.Value));
            Assert.AreEqual("<binary 2 bytes>", ValueDisplay.Format(new byte[] { 1, 2 }));
            Assert.AreEqual("12.5", ValueDisplay.Format(12.5m));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
