namespace Dot.Raft.Testing.Utilities;

public class LogicalHeartbeatTimer(int interval) : IHeartbeatTimer
{
    private int _elapsed;

    public bool ShouldSendHeartbeat() => ++_elapsed >= interval;
    public void Reset() => _elapsed = 0;
}
