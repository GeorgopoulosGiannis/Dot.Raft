using Shouldly;

namespace Dot.Raft.UnitTests.RaftNodeTests;

public class ElectionTests
{
    private class FakeTransport(Action<NodeId, object> onSend) : IRaftTransport
    {
        public Task SendAsync<T>(NodeId to, T message)
        {
            onSend(to, message);
            return Task.CompletedTask;
        }
    }

    private class FixedElectionTimeout(int ticks) : IElectionTimeoutProvider
    {
        public int GetTimeoutTicks() => ticks;
    }

    [Fact]
    public async Task StartsElection_WhenElectionTimeoutExpires()
    {
        var nodeId = new NodeId(1);
        var peerId = new NodeId(2);
        var sentMessagesPair = new List<(NodeId To, object Message)>();

        var transport = new FakeTransport((to, message) => sentMessagesPair.Add((to, message)));
        // Always 3 ticks
        var timeoutProvider = new FixedElectionTimeout(3);

        var node = new RaftNode(nodeId, [peerId], transport, timeoutProvider);
        
        // Tick 1
        await node.TickAsync();
        // Tick 2
        await node.TickAsync();

        sentMessagesPair.ShouldBeEmpty();

        // Tick 3 -  should trigger election
        await node.TickAsync(); // 3

        sentMessagesPair.Count.ShouldBe(1);
        sentMessagesPair[0].To.ShouldBe(peerId);
        var request = sentMessagesPair[0].Message as RequestVoteRequest;
        request.ShouldNotBeNull();
        request.Term.ShouldBe(new Term(1));
        request.CandidateId.ShouldBe(nodeId);
    }
}