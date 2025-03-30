namespace Dot.Raft;

/// <summary>
/// The arguments required to request a vote.
/// </summary>
public record RequestVote
{
    /// <summary>
    /// Candidate's term.
    /// </summary>
    public required Term Term { get; init; }

    /// <summary>
    /// The candidate requesting the vote.
    /// </summary>
    public required NodeId CandidateId { get; init; }

    /// <summary>
    /// The index of candidate's last log entry.
    /// </summary>
    public required int LastLogIndex { get; init; }

    /// <summary>
    /// The term of candidate's last log entry.
    /// </summary>
    public required Term LastLogTerm { get; init; }
}
