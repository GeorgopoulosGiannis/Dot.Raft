namespace Dot.Raft;

/// <summary>
/// Interface for the RequestVote RPC.
/// </summary>
public interface IRequestVote
{
    /// <summary>
    /// Invoked by candidates to gather votes.
    /// </summary>
    /// <param name="arguments"><see cref="RequestVote"/>.</param>
    Task RequestVoteAsync(RequestVote arguments);
}

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

/// <summary>
/// The result for a <see cref="IRequestVote.InvokeAsync"/> invocation.
/// </summary>
public record RequestVoteResponse
{

    /// <summary>
    /// Current term for candidate to update itself.
    /// </summary>
    public required Term Term { get; init; } = default;

    /// <summary>
    /// True means candidate received vote.
    /// </summary>
    public required bool VoteGranted { get; init; }
    
    /// <summary>
    /// The <see cref="NodeId"/> of the node that sends the reply.
    /// </summary>
    public required NodeId ReplierId { get; init; }
}