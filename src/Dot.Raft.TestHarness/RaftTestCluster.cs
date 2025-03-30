namespace Dot.Raft.TestHarness;

public class RaftTestCluster
{
    private readonly List<TestRaftNodeHost> _hosts = [];
    private readonly InMemoryNetwork _network = new();

    public IRaftTransport Transport => _network;

    public void Join(IRaftNode node)
    {
        var host = new TestRaftNodeHost(node);
        _hosts.Add(host);
        _network.Register(host.Node.Id, host);
    }

    public async Task TickAllAsync(int times = 1)
    {
        for (var i = 0; i < times; i++)
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

    public void VisitNodes(IRaftNodeVisitor visitor)
    {
        foreach (var host in _hosts)
        {
            host.Node.Accept(visitor);
        }
    }

    public async Task SubmitToLeaderAsync(object command)
    {
        var leader = GetLeader();
        await leader.SubmitCommandAsync(command);
    }

    public async Task TickUntilLeaderElected(int maxTicks = 100)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            await TickAllAsync();

            var leaderCount = _hosts.Count(h => h.Node.Role == RaftRole.Leader);
            if (leaderCount == 1)
                return;
        }

        throw new Exception("Leader was not elected in time.");
    }
}