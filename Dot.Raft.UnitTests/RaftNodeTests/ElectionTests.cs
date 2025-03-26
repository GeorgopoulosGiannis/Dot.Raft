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

    [Fact]
    public async Task BecomesLeader_WhenMajorityVotesAreReceived()
    {
        var nodeId = new NodeId(1);
        var peerIds = new[] { new NodeId(2), new NodeId(3), new NodeId(4) };


        var transport = new FakeTransport((_, _) => { });
        var timeoutProvider = new FixedElectionTimeout(1); // 1 tick to trigger immediately

        var node = new RaftNode(nodeId, peerIds.ToList(), transport, timeoutProvider);

        // Trigger election
        await node.TickAsync();

        // Simulate receiving votes from two peers
        var vote1 = new RequestVoteResponse
        {
            Term = new Term(1),
            ReplierId = new NodeId(2),
            VoteGranted = true,
        };
        var vote2 = new RequestVoteResponse
        {
            Term = new Term(1),
            ReplierId = new NodeId(3),
            VoteGranted = true,
        };

        await node.ReceiveAsync(vote1);
        await node.ReceiveAsync(vote2);

        node.Role.ShouldBe(RaftRole.Leader);
    }

    [Fact]
    public async Task BecomesFollower_WhenNewestTermIsSeen()
    {
        var nodeId = new NodeId(1);
        var peerIds = new[] { new NodeId(2), new NodeId(3), new NodeId(4) };


        var transport = new FakeTransport((_, _) => { });
        var timeoutProvider = new FixedElectionTimeout(1);

        var node = new RaftNode(
            nodeId,
            peerIds.ToList(),
            transport,
            new State(),
            timeoutProvider,
            RaftRole.Candidate);

        // Receive vote response with greater term
        var vote1 = new RequestVoteResponse
        {
            Term = new Term(5),
            ReplierId = new NodeId(2),
            VoteGranted = false,
        };

        await node.ReceiveAsync(vote1);

        node.Role.ShouldBe(RaftRole.Follower);
    }
}