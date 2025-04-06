namespace Dot.Raft;

/// <summary>
/// A timer used to determine when a Raft leader should send heartbeat messages
/// to its followers to maintain leadership and prevent election timeouts.
/// </summary>
public class TimeBasedHeartbeatTimer : IHeartbeatTimer
{
    private static readonly Random _random = new();

    private readonly TimeSpan _minInterval;
    private readonly TimeSpan _maxInterval;

    private DateTime _lastResetTime;
    private TimeSpan _currentInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeBasedHeartbeatTimer"/> class.
    /// </summary>
    /// <param name="minInterval">The minimum interval between heartbeats.</param>
    /// <param name="maxInterval">The maximum interval between heartbeats.</param>
    public TimeBasedHeartbeatTimer(TimeSpan minInterval, TimeSpan maxInterval)
    {
        if (minInterval > maxInterval)
        {
            throw new ArgumentException("minInterval must be less than or equal to maxInterval");
        }

        this._minInterval = minInterval;
        this._maxInterval = maxInterval;

        Reset();
    }

    /// <summary>
    /// Determines whether it's time for the leader to send a heartbeat message.
    /// </summary>
    /// <returns><c>true</c> if a heartbeat should be sent; otherwise, <c>false</c>.</returns>
    public bool ShouldSendHeartbeat()
    {
        return DateTime.UtcNow - this._lastResetTime >= this._currentInterval;
    }

    /// <summary>
    /// Resets the heartbeat timer, typically after a heartbeat has been sent.
    /// </summary>
    public void Reset()
    {
        this._lastResetTime = DateTime.UtcNow;
        this._currentInterval = TimeSpan.FromMilliseconds(
            _random.Next((int)this._minInterval.TotalMilliseconds, (int)this._maxInterval.TotalMilliseconds)
        );
    }
}
