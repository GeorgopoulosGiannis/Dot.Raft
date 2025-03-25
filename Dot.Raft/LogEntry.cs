namespace Dot.Raft;

public record LogEntry
{
    public required Term Term { get; init; }

    public required object Command { get; init; }
}