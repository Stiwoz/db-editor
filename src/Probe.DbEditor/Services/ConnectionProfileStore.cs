using System.IO;
using System.Text.Json;
using Probe.DbEditor.Models;

namespace Probe.DbEditor.Services;

public sealed class ConnectionProfileStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ConnectionProfileStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProbeDbEditor");
        _filePath = Path.Combine(directory, "connections.json");
    }

    public async Task<List<ConnectionProfile>> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var profiles = await JsonSerializer.DeserializeAsync<List<StoredConnectionProfile>>(stream, _jsonOptions);
        return profiles?.Select(profile => profile.ToProfile()).ToList() ?? [];
    }

    public async Task SaveAsync(IEnumerable<ConnectionProfile> profiles)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var persistableProfiles = profiles.Select(StoredConnectionProfile.FromProfile).ToList();
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, persistableProfiles, _jsonOptions);
    }
}
