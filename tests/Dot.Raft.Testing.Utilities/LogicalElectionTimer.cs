namespace Dot.Raft.Testing.Utilities;

public class LogicalElectionTimer(int timeout) : IElectionTimer
{
    private int _elapsed = 0;

    public bool ShouldTriggerElection() => ++_elapsed >= timeout;
    public void Reset() => _elapsed = 0;
}