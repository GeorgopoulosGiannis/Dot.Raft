namespace Dot.Raft.TestHarness.Tests.ScenarioBuilding;

/// <summary>
/// A generic implementation of <see cref="IRaftNodeVisitor"/> that allows inline logic using a lambda or delegate.
/// Useful for ad-hoc inspections or assertions in Raft test scenarios.
/// </summary>
/// <param name="action">
/// The action to execute for each visited node, receiving the node's ID, current term,
/// role, state, and associated state machine.
/// </param>
public class LinqVisitor(
    Action<NodeId, Term, RaftRole, State, IStateMachine> action
) : IRaftNodeVisitor
{
    /// <summary>
    /// Visits a node and invokes the provided action delegate.
    /// </summary>
    /// <typeparam name="TStateMachine">The concrete type of the state machine.</typeparam>
    /// <param name="id">The ID of the Raft node being visited.</param>
    /// <param name="term">The current term of the node.</param>
    /// <param name="role">The role of the node (Follower, Candidate, or Leader).</param>
    /// <param name="state">The internal Raft state object of the node.</param>
    /// <param name="stateMachine">The state machine associated with the node.</param>
    public void Visit<TStateMachine>(
        NodeId id,
        Term term,
        RaftRole role,
        State state,
        TStateMachine stateMachine)
        where TStateMachine : IStateMachine
    {
        action(id, term, role, state, stateMachine);
    }
}