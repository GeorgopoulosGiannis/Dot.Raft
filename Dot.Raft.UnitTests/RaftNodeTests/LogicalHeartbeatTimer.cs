namespace Dot.Raft.UnitTests.RaftNodeTests;

public class LogicalHeartbeatTimer(int interval) : IHeartbeatTimer
{
    private int _elapsed = 0;

    public bool ShouldSendHeartbeat() => ++_elapsed >= interval;
    public void Reset() => _elapsed = 0;
}