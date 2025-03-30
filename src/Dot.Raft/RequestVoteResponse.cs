namespace Dot.Raft;

/// <summary>
/// The result for a Request Vote RPC invocation.
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
