using Probe.DbEditor.Models;
using Probe.DbEditor.Services;

namespace Probe.DbEditor.Tests.Services;

[TestClass]
public sealed class ConnectionFailureMessageTests
{
    [TestMethod]
    public void Create_FormatsDatabaseTimeout()
    {
        var message = ConnectionFailureMessage.Create(
            new OperationCanceledException(),
            ConnectionProtocolKind.Tcp);

        Assert.AreEqual(
            "Connection timed out after 10 seconds. Check the database endpoint.",
            message);
    }

    [TestMethod]
    public void Create_FormatsSshTimeout()
    {
        var message = ConnectionFailureMessage.Create(
            new OperationCanceledException(),
            ConnectionProtocolKind.SshTunnel);

        Assert.AreEqual(
            "Connection timed out after 10 seconds. Check the SSH tunnel or forwarded database endpoint.",
            message);
    }

    [TestMethod]
    public void Create_UnwrapsSingleInnerAggregateException()
    {
        var message = ConnectionFailureMessage.Create(
            new AggregateException(new InvalidOperationException("SSH host is required.")),
            ConnectionProtocolKind.SshTunnel);

        Assert.AreEqual("SSH host is required.", message);
    }
}
