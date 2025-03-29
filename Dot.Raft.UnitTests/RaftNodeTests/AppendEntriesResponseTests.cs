using Shouldly;

namespace Dot.Raft.UnitTests.RaftNodeTests;

public class AppendEntriesResponseTests
{
    [Fact]
    public async Task LeaderAdvancesMatchIndex_WhenFollowerAppendsSuccessfully()
    {
        var peers = new List<NodeId> { new NodeId(2), new NodeId(3) };
        var state = new State { CurrentTerm = new Term(5) };
        state.AddLogEntry(new Term(5), "set x");

        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            peers,
            transport,
            state,
            new FixedElectionTimeout(1),
            new DummyStateMachine());

        // become leader
        await node.TickAsync();

        await node.ReceivePeerMessageAsync(peers[1],
            new RequestVoteResponse { Term = new Term(6), VoteGranted = true });

        // Start append entries
        await node.TickAsync();

        // follower 2 confirms replication of entry at index 0
        var response = new AppendEntriesResponse
        {
            Term = new Term(6),
            Success = true
        };

        await node.ReceivePeerMessageAsync(peers[1], response);

        var followerIndex = peers.IndexOf(peers[1]);
        state.MatchIndexes[followerIndex].ShouldBe(0);
        state.NextIndexes[followerIndex].ShouldBe(1);
    }

    [Fact]
    public async Task LeaderCommitsEntry_WhenReplicatedOnMajority()
    {
        var peers = new List<NodeId> { new NodeId(2), new NodeId(3) };
        var state = new State { CurrentTerm = new Term(5) };

        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            peers,
            transport,
            state,
            new FixedElectionTimeout(1),
            new DummyStateMachine());

        // become leader
        await node.TickAsync();
        await node.ReceivePeerMessageAsync(peers[0],
            new RequestVoteResponse { Term = new Term(6), VoteGranted = true });
        await node.ReceivePeerMessageAsync(peers[1],
            new RequestVoteResponse { Term = new Term(6), VoteGranted = true });

        // submit a new command (term 6)
        await node.SubmitCommandAsync("set x");

        // follower 2 confirms replication of entry at index 0
        await node.ReceivePeerMessageAsync(peers[0], new AppendEntriesResponse
        {
            Term = new Term(6),
            Success = true
        });


        // Commit index should now be advanced to 0
        state.CommitIndex.ShouldBe(0);
    }
}