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
            using var document = JsonDocument.Parse(rawJson);
            var storedProfile = document.RootElement.GetProperty("Profiles")[0];
            Assert.IsTrue(storedProfile.GetProperty("ProtectedPassword").GetString()?.Length > 0);

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
    public async Task SaveAndLoadFavoritesAsync_PersistsFoldersAndProfileMetadata()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"probe-db-editor-{Guid.NewGuid():N}", "connections.json");
        var store = new ConnectionProfileStore(filePath);
        var folder = new ConnectionProfileFolder
        {
            Id = "folder-1",
            Name = "Production",
            Color = ConnectionFavoriteColor.Blue
        };
        var profile = new ConnectionProfile
        {
            Id = "profile-1",
            Name = "Primary",
            FolderId = folder.Id,
            Color = ConnectionFavoriteColor.Red
        };

        try
        {
            await store.SaveFavoritesAsync([profile], [folder]);

            var favorites = await store.LoadFavoritesAsync();
            Assert.AreEqual(1, favorites.Folders.Count);
            Assert.AreEqual("folder-1", favorites.Folders[0].Id);
            Assert.AreEqual("Production", favorites.Folders[0].Name);
            Assert.AreEqual(ConnectionFavoriteColor.Blue, favorites.Folders[0].Color);
            Assert.AreEqual(1, favorites.Profiles.Count);
            Assert.AreEqual("profile-1", favorites.Profiles[0].Id);
            Assert.AreEqual("folder-1", favorites.Profiles[0].FolderId);
            Assert.AreEqual(ConnectionFavoriteColor.Red, favorites.Profiles[0].Color);
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
    public async Task LoadFavoritesAsync_LoadsLegacyFlatProfileArray()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"probe-db-editor-{Guid.NewGuid():N}", "connections.json");
        var store = new ConnectionProfileStore(filePath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(
                filePath,
                """
                [
                  {
                    "Id": "legacy-profile",
                    "Name": "Legacy",
                    "Host": "legacy.example.test",
                    "Port": 3306
                  }
                ]
                """);

            var favorites = await store.LoadFavoritesAsync();
            Assert.AreEqual(0, favorites.Folders.Count);
            Assert.AreEqual(1, favorites.Profiles.Count);
            Assert.AreEqual("legacy-profile", favorites.Profiles[0].Id);
            Assert.AreEqual("Legacy", favorites.Profiles[0].Name);
            Assert.AreEqual("", favorites.Profiles[0].FolderId);
            Assert.AreEqual(ConnectionFavoriteColor.None, favorites.Profiles[0].Color);
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
