namespace Dot.Raft;

/// <summary>
/// A randomized election timeout provider to avoid split votes.
/// </summary>
public class RandomizedElectionTimeout : IElectionTimer
{
    private readonly TimeSpan _timeout;
    private DateTime _lastReset;

    /// <summary>
    /// Returns a new instance of <see cref="RandomizedElectionTimeout"/>.
    /// </summary>
    public RandomizedElectionTimeout()
    {
        var random = new Random();
        // A randomized tick window between 150-300.
        _timeout = TimeSpan.FromMilliseconds(random.Next(150, 301));
    }

    /// <summary>
    /// Returns true if the required time has passed to trigger an election.
    /// </summary>
    /// <returns></returns>
    public bool ShouldTriggerElection()
    {
        var now = DateTime.UtcNow;
        if (now - _lastReset < _timeout)
        {
            return false;
        }

        _lastReset = now;
        return true;
    }

    /// <summary>
    /// Resets the timer.
    /// </summary>
    public void Reset()
    {
        _lastReset = DateTime.UtcNow;
    }
}