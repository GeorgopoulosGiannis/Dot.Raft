namespace Dot.Raft;

/// <summary>
/// Represents a timer used to determine when a Raft leader should send heartbeat messages
/// to its followers to maintain leadership and prevent timeouts.
/// </summary>
public interface IHeartbeatTimer
{
    /// <summary>
    /// Determines whether it's time for the leader to send a heartbeat message.
    /// </summary>
    /// <returns><c>true</c> if a heartbeat should be sent; otherwise, <c>false</c>.</returns>
    bool ShouldSendHeartbeat();

    /// <summary>
    /// Resets the heartbeat timer, typically after a heartbeat has been sent.
    /// </summary>
    void Reset();
}
