namespace Probe.DbEditor.Models;

public sealed class ConnectionProfileFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New folder";
    public ConnectionFavoriteColor Color { get; set; }
}
