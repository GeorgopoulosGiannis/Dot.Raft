namespace Dot.Raft;

/// <summary>
/// Represents a Raft node capable of participating in the Raft consensus algorithm.
/// Defines the core operations and properties of a Raft node, including ticking, message handling, and command submission.
/// </summary>
public interface IRaftNode
{
    /// <summary>
    /// Gets the unique identifier of the Raft node.
    /// </summary>
    NodeId Id { get; }

    /// <summary>
    /// Gets the state machine that applies committed log entries.
    /// </summary>
    IStateMachine StateMachine { get; }

    /// <summary>
    /// Gets the current role of the node in the Raft protocol (e.g., Follower, Candidate, Leader).
    /// </summary>
    RaftRole Role { get; }

    /// <summary>
    /// Gets the current term of the node as defined by the Raft protocol.
    /// </summary>
    Term CurrentTerm { get; }

    /// <summary>
    /// Advances the node’s internal timers and performs time-based operations.
    /// Typically called periodically (e.g., every few milliseconds).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TickAsync();

    /// <summary>
    /// Handles a message received from another node in the cluster.
    /// </summary>
    /// <param name="message">The message object, which could be a Raft RPC (e.g., AppendEntries, RequestVote).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReceivePeerMessageAsync(object message);

    /// <summary>
    /// Submits a command to the Raft log. If the node is the leader, the command will be replicated to followers.
    /// If not the leader, the command may be rejected or redirected.
    /// </summary>
    /// <param name="command">The command to submit to the replicated log.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SubmitCommandAsync(object command);
    
    void Accept(IRaftNodeVisitor visitor);
}