using Dot.Raft.Testing.Utilities;

namespace Dot.Raft.TestHarness.Tests.ClusterTests;

public static class ClusterFactory
{
    public static RaftTestCluster Create(int size)
    {
        var cluster = new RaftTestCluster();
        for (var i = 0; i < size; i++)
        {
            cluster.Join(new RaftNode(
                new NodeId(i + 1),
                Enumerable.Range(1, size)
                    .Where(n => n != i + 1)
                    .Select(n => new NodeId(n))
                    .ToList(),
                cluster.Transport,
                new LogicalElectionTimer(5 + i),
                new LogicalHeartbeatTimer(2 + i),
                new DummyStateMachine()));
        }

        return cluster;
    }
}