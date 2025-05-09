namespace Dot.Raft;

/// <summary>
/// The arguments for an AppendEntries RPC request.
/// </summary>
public record AppendEntries
{
    /// <summary>
    /// Gets leader's term.
    /// </summary>
    public required Term Term { get; init; }

    /// <summary>
    /// Gets so followers can redirect clients.
    /// </summary>
    public required NodeId LeaderId { get; init; }

    /// <summary>
    /// Gets index of log entry immediately preceding new ones.
    /// </summary>
    public required int PrevLogIndex { get; init; }

    /// <summary>
    /// Gets term of <see cref="AppendEntries.PrevLogIndex"/> entry.
    /// </summary>
    public required Term PrevLogTerm { get; init; }

    /// <summary>
    /// Gets log entries to store.
    /// Empty for heartbeat.
    /// May send more than one for efficiency.
    /// </summary>
    public required LogEntry[] Entries { get; init; }

    /// <summary>
    /// Gets leader's commit index.
    /// </summary>
    public required int LeaderCommit { get; init; }

    /// <summary>
    /// The log entries to be appended to the followers log.
    /// </summary>
    /// <param name="Term">The <see cref="Term"/>that this command was decided on.</param>
    /// <param name="Command">The command to append.</param>
    public record LogEntry(Term Term, object Command);
}
