namespace Dot.Raft;

public interface IHeartbeatTimer
{
    bool ShouldSendHeartbeat();
    void Reset();
}