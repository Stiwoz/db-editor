namespace Probe.DbEditor.Models;

public sealed class QueryLogEntry
{
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;
    public string Statement { get; init; } = "";
    public string Parameters { get; init; } = "";
    public bool Success { get; set; }
    public int RowsAffected { get; set; }
    public long DurationMs { get; set; }
    public string Error { get; set; } = "";
}
