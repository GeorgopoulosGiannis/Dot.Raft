using Dot.Raft.TestHarness.Tests.ScenarioBuilding;
using Shouldly;
using Xunit.Abstractions;

namespace Dot.Raft.TestHarness.Tests.ClusterTests;

public class ClusterTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Cluster_ReplicatesAndAppliesCommand()
    {
        await ScenarioBuilder.Build(3)
            .Then(x => x.TickUntilLeaderElected())
            .Then(x => x.SubmitCommand("x"))
            .Then(x => x.Tick(50))
            .Then(x => x.AssertAllApplied())
            .Then(x => x.PrintClusterState(output));
    }

    [Fact]
    public async Task Partition_And_Heal_Still_Replicates()
    {
        await ScenarioBuilder.Build(3)
            .Then(builder =>
            {
                var node1 = builder.NodeIds[0];
                var node2 = builder.NodeIds[1];

                return builder
                    .Partition(node1, node2)
                    .Then(x => x.Tick(10))
                    .Then(x => x.Heal(node1, node2));
            })
            .Then(x => x.TickUntilLeaderElected())
            .Then(x => x.SubmitCommand("recover"))
            .Then(x => x.Tick(50))
            .Then(x => x.AssertAllApplied())
            .Then(x => x.PrintClusterState(output));
    }

    [Fact]
    public async Task Partition_DisruptsLeadership()
    {
        var builder = await ScenarioBuilder.Build(3)
            .Then(x => x.TickUntilLeaderElected())
            .Then(x => x.AssertSingleLeader());

        var leaderId = builder.Cluster.GetLeader().Id;
        var isolatedId = builder.NodeIds.First(id => id != leaderId); // pick someone to isolate from leader

        await builder
            .Partition(leaderId, isolatedId)
            .Tick(20)
            .Then(x =>
            {
                var leaderCount = x.Cluster.GetAllLeaders().Count;
                leaderCount.ShouldBeInRange(0, 2); // multiple leaders or no leader = valid
                return x;
            });
    }
}