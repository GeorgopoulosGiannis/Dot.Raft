using Xunit.Abstractions;

namespace Dot.Raft.TestHarness;

public class ClusterTests(ITestOutputHelper helper)
{
    [Fact]
    public async Task Cluster_ReplicatesAndAppliesCommand()
    {
        var cluster = new RaftTestCluster(3);
        await cluster.TickUntilLeaderElected();

        var leader = cluster.GetLeader();
        await leader.SubmitCommandAsync("hello");

        await cluster.TickAllAsync(100);

        cluster.AssertAllStateMachinesHave("hello");

        cluster.PrintClusterState(helper);
    }
}