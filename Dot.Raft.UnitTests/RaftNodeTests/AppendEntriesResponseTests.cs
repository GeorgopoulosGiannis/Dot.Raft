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
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(1),
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
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(1),
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
    
    
    [Fact]
    public async Task RetriesAppendEntriesWhenFollowerRejects()
    {
        var leaderId = new NodeId(1);
        var followerId = new NodeId(2);
        var transport = new TestTransport();

        var state = new State
        {
            CurrentTerm = new Term(1),
            CommitIndex = 0,
            LastApplied = -1
        };
        state.AddLogEntry(new Term(1), "cmd1");

        var node = new RaftNode(
            leaderId,
            [followerId],
            transport,
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(1),
            new DummyStateMachine());
        
        // promote to leader
        await node.TickAsync();
        await node.ReceivePeerMessageAsync(followerId, new RequestVoteResponse
        {
            VoteGranted = true,
            Term = new Term(2),
        });
        
        await node.ReceivePeerMessageAsync(followerId, new AppendEntriesResponse
        {
            Success = false,
            Term = new Term(2)
        });

        var retry = transport.Sent.LastOrDefault(msg => msg.To == followerId && msg.Message is AppendEntries);
        retry.ShouldNotBeNull();
        var request = retry.Message as AppendEntries;
        request!.PrevLogIndex.ShouldBe(0);
        request.Entries.Length.ShouldBe(1);
    }
}