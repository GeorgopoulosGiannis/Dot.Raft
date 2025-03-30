using Dot.Raft.UnitTests.RaftNodeTests;
using Shouldly;
using Xunit.Abstractions;

namespace Dot.Raft.TestHarness;

public class RaftTestCluster
{
    private readonly List<TestRaftNodeHost> _hosts = new();
    private readonly InMemoryNetwork _network = new();

    public RaftTestCluster(int size)
    {
        for (var i = 0; i < size; i++)
        {
            var id = new NodeId(i + 1);
            var peers = Enumerable.Range(1, size)
                .Where(n => n != i + 1)
                .Select(n => new NodeId(n))
                .ToList();

            var electionTimer = new RandomizedLogicalElectionTimer(i);
            var heartbeatTimer = new RandomizedLogicalHeartbeatTimer(i);
            var stateMachine = new DummyStateMachine();

            var node = new RaftNode(
                id,
                peers,
                _network,
                new State(),
                electionTimer,
                heartbeatTimer,
                stateMachine);

            var host = new TestRaftNodeHost(node);

            _network.Register(id, host);
            _hosts.Add(host);
        }
    }

    public async Task TickAllAsync(int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            foreach (var host in _hosts)
                await host.TickAsync();
        }
    }

    public IRaftNode GetLeader()
    {
        var leaders = _hosts.Select(h => h.Node)
            .Where(n => n.Role == RaftRole.Leader)
            .ToList();

        if (leaders.Count != 1)
        {
            throw new Exception("Expected exactly one leader");
        }

        return leaders[0];
    }

    public void AssertAllStateMachinesHave(params object[] expected)
    {
        foreach (var host in _hosts)
        {
            var applied = ((DummyStateMachine)host.Node.StateMachine).AppliedCommands;
            applied.ShouldBe(expected);
        }
    }

    public async Task SubmitToLeaderAsync(object command)
    {
        var leader = GetLeader();
        await leader.SubmitCommandAsync(command);
    }

    public async Task TickUntilLeaderElected(int maxTicks = 100)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            await TickAllAsync();

            var leaderCount = _hosts.Count(h => h.Node.Role == RaftRole.Leader);
            if (leaderCount == 1)
                return;
        }

        throw new Exception("Leader was not elected in time.");
    }

    public void PrintClusterState(ITestOutputHelper output)
    {
        output.WriteLine("=== Cluster State ===");
        var visitor = new DebugVisitor(output);
        foreach (var host in _hosts)
            host.Node.Accept(visitor);
        output.WriteLine("=====================");
    }
}