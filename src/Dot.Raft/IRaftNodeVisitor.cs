namespace Dot.Raft;

/// <summary>
/// Defines a visitor that can inspect or interact with a Raft node's internal state
/// without modifying the node itself.
/// </summary>
public interface IRaftNodeVisitor
{
    /// <summary>
    /// Visits a Raft node and exposes its internal state in a read-only manner.
    /// </summary>
    /// <typeparam name="TStateMachine">The type of the node's state machine.</typeparam>
    /// <param name="id">The unique identifier of the node.</param>
    /// <param name="term">The current term of the node.</param>
    /// <param name="role">The current role of the node (Follower, Candidate, or Leader).</param>
    /// <param name="state">The internal state of the node, including log, term, commit index, etc.</param>
    /// <param name="stateMachine">The state machine associated with the node that receives committed commands.</param>
    void Visit<TStateMachine>(
        NodeId id,
        Term term,
        RaftRole role,
        State state,
        TStateMachine stateMachine)
        where TStateMachine : IStateMachine;
}
