using Dot.Raft.Testing.Utilities;
using Shouldly;

namespace Dot.Raft.Tests.RaftNodeTests;

public class ReceiveVoteTests
{
    private class FakeTransport : IRaftTransport
    {
        public NodeId? SendTo { get; private set; }
        public object? Command { get; private set; }

        public Task SendAsync(NodeId sendTo, object command)
        {
            SendTo = sendTo;
            Command = command;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ConvertsToFollower_WhenCandidateTermIsHigher()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            new State
            {
                CurrentTerm = new Term(0)
            },
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        await node.TickAsync();

        await node.ReceivePeerMessageAsync(new RequestVoteResponse
        {
            ReplierId = candidateId,
            Term = new Term(1),
            VoteGranted = true
        });

        node.Role.ShouldBe(RaftRole.Leader);

        var request = new RequestVote
        {
            Term = new Term(2),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceivePeerMessageAsync(request);

        node.Role.ShouldBe(RaftRole.Follower);
    }

    [Fact]
    public async Task GrantsVote_WhenCandidateTermIsGreaterAndLogIsUpToDate()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var state = new State();
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new RequestVote
        {
            Term = new Term(1),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceivePeerMessageAsync(request);

        transport.SendTo.ShouldBe(candidateId);
        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeTrue();
        response.Term.ShouldBe(new Term(1));
        state.VotedFor.ShouldBe(candidateId);
    }

    [Fact]
    public async Task GrantsVote_WhenCandidateTermIsHigher_AndLogUpToDate_AndFirstVote()
    {
        var state = new State { CurrentTerm = new Term(2) };
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var node = new RaftNode(
            new NodeId(1),
            [candidateId],
            transport,
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new RequestVote
        {
            Term = new Term(3),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceivePeerMessageAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeTrue();
        response.Term.ShouldBe(new Term(3));
        transport.SendTo.ShouldBe(candidateId);
        state.VotedFor.ShouldBe(candidateId);
    }

    [Fact]
    public async Task GrantsVote_WhenAlreadyVotedForSameCandidateInSameTerm()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var state = new State
        {
            CurrentTerm = new Term(5),
            VotedFor = candidateId
        };
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new RequestVote
        {
            Term = new Term(5),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceivePeerMessageAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeTrue();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
        state.VotedFor.ShouldBe(candidateId);
    }

    [Fact]
    public async Task RejectsVote_WhenCandidateTermIsLessThanCurrentTerm()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var state = new State { CurrentTerm = new Term(5) };
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());


        var request = new RequestVote
        {
            Term = new Term(4),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceivePeerMessageAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
        state.VotedFor.ShouldNotBe(candidateId);
    }

    [Fact]
    public async Task RejectsVote_WhenAlreadyVotedForAnotherCandidateInSameTerm()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var alreadyVotedFor = new NodeId(3);
        var transport = new FakeTransport();
        var state = new State
        {
            CurrentTerm = new Term(5),
            VotedFor = alreadyVotedFor
        };
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new RequestVote
        {
            Term = new Term(5),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };
        await node.ReceivePeerMessageAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
        state.VotedFor.ShouldBe(alreadyVotedFor);
    }

    [Fact]
    public async Task RejectsVote_WhenCandidateLogIsOutdated()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();

        var state = new State
        {
            CurrentTerm = new Term(5)
        };
        state.AddLogEntry(new Term(5), "set x = 1");
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            state,
            new LogicalElectionTimer(1),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new RequestVote
        {
            Term = new Term(5),
            CandidateId = candidateId,
            LastLogIndex = 0, // Candidate index 0 (less than node index 0)
            LastLogTerm = new Term(4) // Candidate term 4 (less than node's term 5)
        };

        await node.ReceivePeerMessageAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
        state.VotedFor.ShouldBeNull();
    }
}