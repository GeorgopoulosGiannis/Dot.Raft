namespace Dot.Raft;

/// <summary>
/// The <see cref="IRaftNode"/> role.
/// </summary>
public enum RaftRole
{
    /// <summary>
    /// The node is in follower mode, and will redirect requests to leader.
    /// </summary>
    Follower,

    /// <summary>
    /// The node is trying to elect as a new leader.
    /// </summary>
    Candidate,

    /// <summary>
    /// The node is acting as a leader, accepting commands from clients.
    /// </summary>
    Leader,
}
