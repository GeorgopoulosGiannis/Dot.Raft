namespace Dot.Raft;

/// <summary>
/// The result for a Request Vote RPC invocation.
/// </summary>
public record RequestVoteResponse
{
    /// <summary>
    /// Gets current term for candidate to update itself.
    /// </summary>
    public required Term Term { get; init; }

    /// <summary>
    /// Gets a value indicating whether true means candidate received vote.
    /// </summary>
    public required bool VoteGranted { get; init; }

    /// <summary>
    /// Gets the <see cref="NodeId"/> of the node that sends the reply.
    /// </summary>
    public required NodeId ReplierId { get; init; }
}
