namespace Dot.Raft;


/// <summary>
/// An interface for a state machine instance that will accept the commited log entries from the <see cref="RaftNode"/>.
/// </summary>
public interface IStateMachine
{
    Task ApplyAsync(object command);
}