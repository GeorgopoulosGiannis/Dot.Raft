namespace Dot.Raft.UnitTests.RaftNodeTests;

public class FixedElectionTimeout(int ticks) : IElectionTimeoutProvider
{
    public int GetTimeoutTicks() => ticks;
}