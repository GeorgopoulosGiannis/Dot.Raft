namespace Dot.Raft;

/// <summary>
/// The append entries operation result returned from the follower.
/// </summary>
public record AppendEntriesResponse
{
    /// <summary>
    /// Gets a value indicating whether the follower contained entry matching prevLogIndex and prevLogTerm.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets currentTerm, for leader to update itself.
    /// </summary>
    public Term Term { get; init; }

    /// <summary>
    /// Gets the <see cref="NodeId"/> of the node sending the reply.
    /// </summary>
    public required NodeId ReplierId { get; init; }
}
