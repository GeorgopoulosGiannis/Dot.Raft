namespace Dot.Raft;

/// <summary>
/// The interface for the AppendEntries RPC.
/// </summary>
public interface IAppendEntries
{
    /// <summary>
    /// Invoked by leader to replicate log entries.
    /// Also used as heartbeat.
    /// The receiver will:
    /// <br/>
    /// 1. Reply false if term &lt; currentTerm.
    /// <br/>
    /// 2. Reply false if log doesn;t contain an entry at prevLogIndex whose term matches prevLogTerm.
    /// <br/>
    /// 3. If any existing entry conflicts with a new one (same index but different terms), delete the existing entry and all that follow it.
    /// <br/>
    /// 4. Append any new entries not already in the log if leaderCommit &gt; commitIndex, set commitIndex = min(leaderCommit,index of last new entry)
    /// </summary>
    /// <returns><see cref="AppendEntriesResponse"/>.</returns>
    Task AppendEntriesAsync(AppendEntries request);
}

/// <summary>
/// The arguments for the <see cref="IAppendEntries.AppendEntriesAsync"/>.
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

/// <summary>
/// The append entries operation result returned from the follower.
/// </summary>
public record AppendEntriesResponse
{
    /// <summary>
    /// True if follower contained entry matching prevLogIndex and prevLogTerm.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// currentTerm, for leader to update itself.
    /// </summary>
    public Term Term { get; init; }
}