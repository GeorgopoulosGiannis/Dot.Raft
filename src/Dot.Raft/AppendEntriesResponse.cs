namespace Dot.Raft;

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

    /// <summary>
    /// The <see cref="NodeId"/> of the node sending the reply.
    /// </summary>
    public required NodeId ReplierId { get; init; }
}
