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
    /// Submits a client command to the Raft log for replication and execution.
    /// The command must be uniquely identified by a combination of <c>ClientId</c> and <c>SequenceNumber</c>
    /// to support deduplication and ensure at-most-once execution semantics.
    /// <para>
    /// If the current node is the leader, the command will be appended to its log and replicated to a majority of followers.
    /// Once committed, it will be applied to the state machine and the result will be returned.
    /// </para>
    /// <para>
    /// If the node is not the leader, the command will be rejected or redirected, depending on implementation.
    /// </para>
    /// </summary>
    /// <param name="command">The client-wrapped command containing metadata for deduplication and the actual payload.</param>
    /// <returns>
    /// A task that completes with the result of executing the command, or <c>null</c> if the node is not the leader.
    /// </returns>
    Task<object?> SubmitCommandAsync(ClientCommandEnvelope command);

    /// <summary>
    /// Accepts a visitor and calls visit with the nodes internal state.
    /// </summary>
    /// <param name="visitor">The <see cref="IRaftNodeVisitor"/> to accept.</param>
    void Accept(IRaftNodeVisitor visitor);
}
