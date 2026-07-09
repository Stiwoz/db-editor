namespace Probe.DbEditor.Models;

public sealed class ConnectionFavorites
{
    public List<ConnectionProfileFolder> Folders { get; } = [];
    public List<ConnectionProfile> Profiles { get; } = [];
}
