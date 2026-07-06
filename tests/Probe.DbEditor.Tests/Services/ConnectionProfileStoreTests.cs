using System.Text.Json;
using Probe.DbEditor.Models;
using Probe.DbEditor.Services;

namespace Probe.DbEditor.Tests.Services;

[TestClass]
public sealed class ConnectionProfileStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadAsync_PersistsOnlyOptedInSecretsUsingProtectedValues()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"probe-db-editor-{Guid.NewGuid():N}", "connections.json");
        var store = new ConnectionProfileStore(filePath);
        var profile = new ConnectionProfile
        {
            Id = "profile-1",
            Name = "Production",
            Password = "database-secret",
            SavePassword = true,
            SshPassword = "ssh-secret",
            SaveSshPassword = true,
            SshPrivateKeyPassphrase = "never-persist-this"
        };

        try
        {
            await store.SaveAsync([profile]);

            var rawJson = await File.ReadAllTextAsync(filePath);
            Assert.IsFalse(rawJson.Contains("database-secret", StringComparison.Ordinal));
            Assert.IsFalse(rawJson.Contains("ssh-secret", StringComparison.Ordinal));
            Assert.IsFalse(rawJson.Contains("never-persist-this", StringComparison.Ordinal));
            Assert.IsTrue(JsonDocument.Parse(rawJson).RootElement[0].GetProperty("ProtectedPassword").GetString()?.Length > 0);

            var loadedProfiles = await store.LoadAsync();
            Assert.AreEqual(1, loadedProfiles.Count);
            var loaded = loadedProfiles[0];
            Assert.AreEqual("database-secret", loaded.Password);
            Assert.IsTrue(loaded.SavePassword);
            Assert.AreEqual("ssh-secret", loaded.SshPassword);
            Assert.IsTrue(loaded.SaveSshPassword);
            Assert.AreEqual("", loaded.SshPrivateKeyPassphrase);
        }
        finally
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_DoesNotPersistSecretsWhenSaveFlagsAreDisabled()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"probe-db-editor-{Guid.NewGuid():N}", "connections.json");
        var store = new ConnectionProfileStore(filePath);
        var profile = new ConnectionProfile
        {
            Password = "database-secret",
            SavePassword = false,
            SshPassword = "ssh-secret",
            SaveSshPassword = false
        };

        try
        {
            await store.SaveAsync([profile]);

            var rawJson = await File.ReadAllTextAsync(filePath);
            Assert.IsFalse(rawJson.Contains("database-secret", StringComparison.Ordinal));
            Assert.IsFalse(rawJson.Contains("ssh-secret", StringComparison.Ordinal));

            var loadedProfiles = await store.LoadAsync();
            Assert.AreEqual(1, loadedProfiles.Count);
            var loaded = loadedProfiles[0];
            Assert.AreEqual("", loaded.Password);
            Assert.IsFalse(loaded.SavePassword);
            Assert.AreEqual("", loaded.SshPassword);
            Assert.IsFalse(loaded.SaveSshPassword);
        }
        finally
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
