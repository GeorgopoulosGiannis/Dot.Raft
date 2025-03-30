namespace Dot.Raft.TestHarness;

public class RandomizedLogicalElectionTimer : IElectionTimer
{
    public RandomizedLogicalElectionTimer(int seed)
    {
        var random = new Random(seed);
        _timeout = random.Next(10, 30);
    }

    private readonly int _timeout = 0;
    private int _elapsed = 0;

    public bool ShouldTriggerElection() => ++_elapsed >= _timeout;
    public void Reset() => _elapsed = 0;
}