using Probe.DbEditor.Models;

namespace Probe.DbEditor.Services;

internal sealed class StoredConnectionProfileFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New folder";
    public ConnectionFavoriteColor Color { get; set; }

    public static StoredConnectionProfileFolder FromFolder(ConnectionProfileFolder folder)
    {
        return new StoredConnectionProfileFolder
        {
            Id = folder.Id,
            Name = folder.Name,
            Color = folder.Color
        };
    }

    public ConnectionProfileFolder ToFolder()
    {
        return new ConnectionProfileFolder
        {
            Id = Id,
            Name = Name,
            Color = Color
        };
    }
}
