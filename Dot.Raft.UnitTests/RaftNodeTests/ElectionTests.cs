using Shouldly;

namespace Dot.Raft.UnitTests.RaftNodeTests;

public class ElectionTests
{
    [Fact]
    public async Task StartsElection_WhenElectionTimeoutExpires()
    {
        var nodeId = new NodeId(1);
        var peerId = new NodeId(2);
        var sentMessagesPair = new List<(NodeId To, object Message)>();

        var transport = new TestTransportWithCallback((to, message) => sentMessagesPair.Add((to, message)));
        // Always 3 ticks

        var node = new RaftNode(
            nodeId,
            [peerId],
            transport,
            new LogicalElectionTimer(3),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        // Tick 1
        await node.TickAsync();
        // Tick 2
        await node.TickAsync();

        sentMessagesPair.ShouldBeEmpty();

        // Tick 3 -  should trigger election
        await node.TickAsync(); // 3

        sentMessagesPair.Count.ShouldBe(1);
        sentMessagesPair[0].To.ShouldBe(peerId);
        var request = sentMessagesPair[0].Message as RequestVote;
        request.ShouldNotBeNull();
        request.Term.ShouldBe(new Term(1));
        request.CandidateId.ShouldBe(nodeId);
    }

    [Fact]
    public async Task BecomesLeader_WhenMajorityVotesAreReceived()
    {
        var node = new RaftNode(
            new NodeId(1),
            [
                new NodeId(2), new NodeId(3), new NodeId(4)
            ],
            new TestTransportWithCallback((_, _) => { }),
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        // Trigger election
        await node.TickAsync();

        // Simulate receiving votes from two peers
        var vote1 = new RequestVoteResponse
        {
            Term = new Term(1),
            VoteGranted = true,
        };
        var vote2 = new RequestVoteResponse
        {
            Term = new Term(1),
            VoteGranted = true,
        };

        await node.ReceivePeerMessageAsync(new NodeId(2), vote1);
        await node.ReceivePeerMessageAsync(new NodeId(3), vote2);

        node.Role.ShouldBe(RaftRole.Leader);
    }

    [Fact]
    public async Task BecomesFollower_WhenNewestTermIsSeen()
    {
        var node = new RaftNode(
            new NodeId(1),
            [
                new NodeId(2), new NodeId(3), new NodeId(4)
            ],
            new TestTransportWithCallback((_, _) => { }),
            new State(),
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());
        await node.TickAsync();

        // Receive vote response with greater term
        var vote1 = new RequestVoteResponse
        {
            Term = new Term(5),
            VoteGranted = false,
        };

        await node.ReceivePeerMessageAsync(new NodeId(2), vote1);

        node.Role.ShouldBe(RaftRole.Follower);
    }

    [Fact]
    public async Task InitializesLeaderState_WhenBecomingLeader()
    {
        var state = new State();
        state.AddLogEntry(new Term(1), "init");

        var node = new RaftNode(
            new NodeId(1),
            [
                new NodeId(2), new NodeId(3)
            ],
            new TestTransportWithCallback((_, _) => { }),
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        // simulate election timeout
        await node.TickAsync();

        // receive 2 votes (majority of 3)
        await node.ReceivePeerMessageAsync(new NodeId(2), new RequestVoteResponse
            { Term = new Term(1), VoteGranted = true });
        await node.ReceivePeerMessageAsync(new NodeId(3), new RequestVoteResponse
            { Term = new Term(1), VoteGranted = true });

        node.Role.ShouldBe(RaftRole.Leader);
        state.NextIndexes.Count.ShouldBe(2);
        state.MatchIndexes.Count.ShouldBe(2);

        foreach (var nextIndex in state.NextIndexes)
        {
            nextIndex.ShouldBe(0); // last log index + 1
        }

        foreach (var matchIndex in state.MatchIndexes)
        {
            matchIndex.ShouldBe(-1);
        }
    }


    [Fact]
    public async Task SendsHeartbeats_WhenLeaderTicks()
    {
        var state = new State();
        state.AddLogEntry(new Term(1), "init");

        var sent = new List<(NodeId, object)>();
        var node = new RaftNode(
            new NodeId(1),
            [
                new NodeId(2), new NodeId(3)
            ],
            new TestTransportWithCallback((nodeId, message) => { sent.Add((nodeId, message)); }),
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(1),
            new DummyStateMachine());

        // become leader
        await node.TickAsync();
        await node.ReceivePeerMessageAsync(new NodeId(2), new RequestVoteResponse
            { Term = new Term(1), VoteGranted = true });
        await node.ReceivePeerMessageAsync(new NodeId(3), new RequestVoteResponse
            { Term = new Term(1), VoteGranted = true });

        sent.Clear();

        // trigger heartbeat
        await node.TickAsync();

        sent.Count.ShouldBe(2);
        foreach (var (_, msg) in sent)
        {
            msg.ShouldBeOfType<AppendEntries>();
            var append = (AppendEntries)msg;

            append.Entries.Length.ShouldBe(1);
            append.Term.ShouldBe(new Term(1));
            append.LeaderId.ShouldBe(new NodeId(1));
            append.LeaderCommit.ShouldBe(state.CommitIndex);

            append.PrevLogIndex.ShouldBe(0);
            append.PrevLogTerm.ShouldBe(new Term(1));
        }
    }

    [Fact]
    public async Task FollowerAcceptsHeartbeat_AndUpdatesCommitIndex()
    {
        var state = new State
        {
            CurrentTerm = new Term(2),
            CommitIndex = 0,
        };

        state.AddLogEntry(new Term(1), "a");
        state.AddLogEntry(new Term(2), "b");

        var sent = new List<(NodeId id, object msg)>();
        var node = new RaftNode(
            new NodeId(1),
            [],
            new TestTransportWithCallback((nodeId, message) => { sent.Add((nodeId, message)); }),
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(2),
            LeaderId = new NodeId(99),
            PrevLogIndex = 1,
            PrevLogTerm = new Term(2),
            Entries = [],
            LeaderCommit = 1
        };

        await node.ReceivePeerMessageAsync(request.LeaderId, request);

        state.CommitIndex.ShouldBe(1);

        sent.Count.ShouldBe(1);
        var response = sent[0].msg as AppendEntriesResponse;
        response.ShouldNotBeNull();
        response!.Success.ShouldBeTrue();
        response.Term.ShouldBe(new Term(2));
    }
}