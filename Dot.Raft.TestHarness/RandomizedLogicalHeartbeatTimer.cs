namespace Dot.Raft.TestHarness;

public class RandomizedLogicalHeartbeatTimer : IHeartbeatTimer
{
    public RandomizedLogicalHeartbeatTimer(int seed)
    {
        var random = new Random(seed);
        _interval = random.Next(1, 5);
    }

    private readonly int _interval = 0;
    private int _elapsed = 0;

    public bool ShouldSendHeartbeat() => ++_elapsed >= _interval;
    public void Reset() => _elapsed = 0;
}