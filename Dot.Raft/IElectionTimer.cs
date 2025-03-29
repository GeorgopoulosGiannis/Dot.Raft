namespace Dot.Raft;

/// <summary>
/// Represents an abstraction over the election timeout timer.
/// Used by Raft nodes to determine when an election should be triggered.
/// </summary>
public interface IElectionTimer
{
    /// <summary>
    /// Determines whether the election timeout has elapsed
    /// and the node should trigger a new election.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the election timeout has elapsed and an election should be triggered;
    /// otherwise, <c>false</c>.
    /// </returns>
    bool ShouldTriggerElection();

    /// <summary>
    /// Resets the election timer.
    /// Typically called when the node receives a heartbeat or starts a new term.
    /// </summary>
    void Reset();
}