namespace Probe.DbEditor.Services;

internal sealed class StoredConnectionFavorites
{
    public List<StoredConnectionProfileFolder> Folders { get; set; } = [];
    public List<StoredConnectionProfile> Profiles { get; set; } = [];
}
