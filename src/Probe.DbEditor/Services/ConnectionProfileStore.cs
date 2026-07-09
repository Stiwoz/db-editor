using System.IO;
using System.Text.Json;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Services;

public sealed class ConnectionProfileStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ConnectionProfileStore()
        : this(CreateDefaultFilePath())
    {
    }

    internal ConnectionProfileStore(string filePath)
    {
        _filePath = filePath;
    }

    private static string CreateDefaultFilePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProbeDbEditor");
        return Path.Combine(directory, "connections.json");
    }

    public async Task<List<ConnectionProfile>> LoadAsync()
    {
        return (await LoadFavoritesAsync()).Profiles;
    }

    public async Task<ConnectionFavorites> LoadFavoritesAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new ConnectionFavorites();
        }

        await using var stream = File.OpenRead(_filePath);
        using var document = await JsonDocument.ParseAsync(stream);
        var favorites = new ConnectionFavorites();
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var profiles = document.RootElement.Deserialize<List<StoredConnectionProfile>>(_jsonOptions) ?? [];
            favorites.Profiles.AddRange(profiles.Select(profile => profile.ToProfile()));
            return favorites;
        }

        var storedFavorites = document.RootElement.Deserialize<StoredConnectionFavorites>(_jsonOptions);
        if (storedFavorites is null)
        {
            return favorites;
        }

        favorites.Folders.AddRange(storedFavorites.Folders.Select(folder => folder.ToFolder()));
        favorites.Profiles.AddRange(storedFavorites.Profiles.Select(profile => profile.ToProfile()));
        return favorites;
    }

    public async Task SaveAsync(IEnumerable<ConnectionProfile> profiles)
    {
        await SaveFavoritesAsync(profiles, []);
    }

    public async Task SaveFavoritesAsync(
        IEnumerable<ConnectionProfile> profiles,
        IEnumerable<ConnectionProfileFolder> folders)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var persistableProfiles = profiles.Select(StoredConnectionProfile.FromProfile).ToList();
        var persistableFolders = folders.Select(StoredConnectionProfileFolder.FromFolder).ToList();
        var favorites = new StoredConnectionFavorites
        {
            Folders = persistableFolders,
            Profiles = persistableProfiles
        };

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, favorites, _jsonOptions);
    }
}
