namespace Dot.Raft;

/// <summary>
/// The arguments required to request a vote.
/// </summary>
public record RequestVote
{
    /// <summary>
    /// Gets candidate's term.
    /// </summary>
    public required Term Term { get; init; }

    /// <summary>
    /// Gets the candidate requesting the vote.
    /// </summary>
    public required NodeId CandidateId { get; init; }

    /// <summary>
    /// Gets the index of candidate's last log entry.
    /// </summary>
    public required int LastLogIndex { get; init; }

    /// <summary>
    /// Gets the term of candidate's last log entry.
    /// </summary>
    public required Term LastLogTerm { get; init; }
}
