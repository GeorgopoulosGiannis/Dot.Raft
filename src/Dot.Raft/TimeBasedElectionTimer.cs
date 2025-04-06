namespace Dot.Raft;

/// <summary>
/// An election timer that triggers after a randomized timeout between the specified minimum and maximum values.
/// This helps reduce the likelihood of split votes in Raft by desynchronizing election triggers.
/// </summary>
public class TimeBasedElectionTimer : IElectionTimer
{
    private static readonly Random _random = new();

    private readonly TimeSpan _minTimeout;
    private readonly TimeSpan _maxTimeout;

    private DateTime _lastResetTime;
    private TimeSpan _currentTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeBasedElectionTimer"/> class with the given timeout range.
    /// </summary>
    /// <param name="minTimeout">The minimum timeout before an election can be triggered.</param>
    /// <param name="maxTimeout">The maximum timeout before an election must be triggered.</param>
    public TimeBasedElectionTimer(TimeSpan minTimeout, TimeSpan maxTimeout)
    {
        if (minTimeout > maxTimeout)
        {
            throw new ArgumentException("minTimeout must be less than or equal to maxTimeout");
        }

        this._minTimeout = minTimeout;
        this._maxTimeout = maxTimeout;

        Reset();
    }

    /// <summary>
    /// Checks whether the election timeout has elapsed.
    /// </summary>
    /// <returns><c>true</c> if the timeout has elapsed and an election should be triggered; otherwise, <c>false</c>.</returns>
    public bool ShouldTriggerElection()
    {
        return DateTime.UtcNow - this._lastResetTime >= this._currentTimeout;
    }

    /// <summary>
    /// Resets the election timer, randomizing the next timeout duration between the configured range.
    /// </summary>
    public void Reset()
    {
        this._lastResetTime = DateTime.UtcNow;
        this._currentTimeout = TimeSpan.FromMilliseconds(
            _random.Next((int)this._minTimeout.TotalMilliseconds, (int)this._maxTimeout.TotalMilliseconds)
        );
    }
}
