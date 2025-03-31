namespace Dot.Raft;

/// <summary>
/// An interface for a state machine instance that will accept the commited log entries from the <see cref="RaftNode"/>.
/// </summary>
public interface IStateMachine
{
    /// <summary>
    /// Called from <see cref="IRaftNode"/> when a command is commited and should be applied
    /// to the state machine.
    /// </summary>
    /// <param name="command">The command to apply. Raft is command type agnostic.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ApplyAsync(object command);
}
