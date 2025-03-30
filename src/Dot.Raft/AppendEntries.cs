namespace Dot.Raft;

/// <summary>
/// The arguments for an AppendEntries RPC request.
/// </summary>
public record AppendEntries
{
    /// <summary>
    /// Leader's term.
    /// </summary>
    public required Term Term { get; init; }

    /// <summary>
    /// So followers can redirect clients.
    /// </summary>
    public required NodeId LeaderId { get; init; }

    /// <summary>
    /// Index of log entry immediately preceding new ones.
    /// </summary>
    public required int PrevLogIndex { get; init; }

    /// <summary>
    /// Term of <see cref="AppendEntries.PrevLogIndex"/> entry.
    /// </summary>
    public required Term PrevLogTerm { get; init; }

    /// <summary>
    /// Log entries to store.
    /// Empty for heartbeat.
    /// May send more than one for efficiency.
    /// </summary>
    public required LogEntry[] Entries { get; init; }

    /// <summary>
    /// Leader's commit index.
    /// </summary>
    public required int LeaderCommit { get; init; }
}
