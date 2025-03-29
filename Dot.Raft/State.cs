namespace Dot.Raft;

/// <summary>
/// The state of a <see cref="RaftNode"/>.
/// </summary>
public class State
{
    #region Persisted - The persistent state of a Raft node. It is updated on stable storage before responding to RPCs.

    /// <summary>
    /// Latest term server has seen (initialized to 0 on first boot, increases monotonically).
    /// </summary>
    public Term CurrentTerm { get; set; } = default;

    /// <summary>
    /// CandidateId that received vote in current term (or null if none).
    /// </summary>
    public NodeId? VotedFor { get; set; }

    /// <summary>
    /// Determines if a candidate's log is at least as up-to-date as the local log,
    /// based on Raft's log up-to-date check.
    /// </summary>
    /// <param name="candidateLastIndex">The last log index of the candidate.</param>
    /// <param name="candidateLastTerm">The last log term of the candidate.</param>
    /// <returns><c>true</c> if the candidate's log is up-to-date; otherwise, <c>false</c>.</returns>
    public bool IsCandidateLogUpToDate(int candidateLastIndex, Term candidateLastTerm)
    {
        if (LogEntries.Count == 0)
        {
            return true;
        }


        var lastLocalLogTerm = LogEntries[^1].Term;
        var lastLocalLogIndex = LogEntries.Count - 1;

        if (candidateLastTerm > lastLocalLogTerm)
        {
            return true;
        }

        if (candidateLastTerm < lastLocalLogTerm)
        {
            return false;
        }

        return candidateLastIndex >= lastLocalLogIndex;
    }

    /// <summary>
    /// Checks whether the log contains an entry at the specified index with the specified term.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <param name="term">The term to match.</param>
    /// <returns><c>true</c> if the entry exists and its term matches; otherwise, <c>false</c>.</returns>
    public bool HasMatchingEntry(int index, Term term)
    {
        if (index < 0 || index >= LogEntries.Count)
            return false;

        return LogEntries[index].Term == term;
    }


    /// <summary>
    /// Log entries; Each entry contains command for state machine,
    /// and term when entry was received by leader.
    /// Paper states that starting index is 1 but we keep it 0 based.
    /// </summary>
    private List<LogEntry> LogEntries { get; init; } = [];


    /// <summary>
    /// Returns the last log index, will return -1 if log is empty.
    /// </summary>
    /// <returns>The index of the last log entry, -1 if no log entries.</returns>
    public int GetLastLogIndex()
    {
        return LogEntries.Count - 1;
    }

    /// <summary>
    /// Returns the number of log entries in the log.
    /// </summary>
    /// <returns>The number of log entries.</returns>
    public int GetCount()
    {
        return LogEntries.Count;
    }

    /// <summary>
    /// Gets the term of the last log entry, or <c>Term(0)</c> if the log is empty.
    /// </summary>
    /// <returns>The last <see cref="Term"/> in the log, or a new <see cref="Term"/> with value 0 if no entries exist.</returns>
    public Term GetLastLogTerm()
    {
        return LogEntries.Count > 0
            ? LogEntries[^1].Term
            : new Term(0);
    }

    /// <summary>
    /// Will return <see cref="Term"/> at specified index,
    /// or 0 if no <see cref="Term"/> exists at index.
    /// </summary>
    /// <param name="index">The index to return the <see cref="Term"/> for.</param>
    /// <returns>The <see cref="Term"/>.</returns>
    public Term GetTermAtIndex(int index)
    {
        return index > LogEntries.Count - 1 ? new Term(0) : LogEntries[index].Term;
    }

    /// <summary>
    /// Returns the command at index, or null if index is out of bounds
    /// </summary>
    /// <param name="index">The index of the log to look.</param>
    /// <returns>The command at index, or the default value.</returns>
    public object? GetCommandAtIndex(int index)
    {
        return index > LogEntries.Count - 1
            ? null
            : LogEntries[index].Command;
    }

    /// <summary>
    /// Returns an enumerable of log entries as tuples containing the <see cref="Term"/> and the associated command,
    /// starting after the specified index.
    /// </summary>
    /// <param name="fromIndex">The zero-based index to start retrieving entries from. Entries before this index are skipped.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of tuples, each containing a <see cref="Term"/> and a command.</returns>
    public IEnumerable<(Term Term, object Command)> GetLogEntries(int fromIndex = 0)
    {
        return LogEntries.Skip(fromIndex).Select(x => (x.Term, x.Command));
    }

    /// <summary>
    /// Appends a new log entry with the specified term and command.
    /// </summary>
    /// <param name="term">The <see cref="Term"/> associated with the command.</param>
    /// <param name="command">The command to append to the log.</param>
    public void AddLogEntry(Term term, object command)
    {
        LogEntries.Add(new LogEntry(term, command));
    }


    /// <summary>
    /// Removes all entries from the specified index to the end of the log.
    /// </summary>
    /// <param name="startIndex">The index from which to remove entries.</param>
    public void RemoveEntriesFrom(int startIndex)
    {
        if (startIndex < LogEntries.Count)
        {
            LogEntries.RemoveRange(startIndex, LogEntries.Count - startIndex);
        }
    }

    #endregion

    #region Volatile - Volatile state on nodes. Reinitialized after election.

    /// <summary>
    /// Index of the highest log entry known to be commited.
    /// Initialized to 0, increases monotonically.
    /// </summary>
    public int CommitIndex { get; set; } = -1;

    /// <summary>
    /// Index of the highest log entry applied to state machine.
    /// Initialized to 0, increases monotonically.
    /// </summary>
    public int LastApplied { get; set; } = -1;

    #endregion

    #region Leader Volatile

    /// <summary>
    /// For each server, index of the next log entry to send to that server.
    /// Initialized to leader last log index + 1.
    /// </summary>
    public List<int> NextIndexes { get; init; } = [];

    /// <summary>
    /// For each server, index of highest log entry known to be replicated on server.
    /// Initialized to 0, increases monotonically.
    /// </summary>
    public List<int> MatchIndexes { get; init; } = [];

    #endregion
}