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
    /// <returns>Anything that needs to be returned from the application. Raft does not care about this value.</returns>
    Task<object> ApplyAsync(object command);
}
