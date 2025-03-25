using Shouldly;

namespace Dot.Raft.UnitTests;

public class RaftNodeTests
{
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
            new RandomizedElectionTimeout(),
            RaftRole.Leader);

        node.Role.ShouldBe(RaftRole.Leader);
        var request = new RequestVoteRequest
        {
            Term = new Term(1),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        node.Role.ShouldBe(RaftRole.Follower);
    }

    [Fact]
    public async Task GrantsVote_WhenCandidateTermIsGreaterAndLogIsUpToDate()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            new RandomizedElectionTimeout());

        var request = new RequestVoteRequest
        {
            Term = new Term(1),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        transport.SendTo.ShouldBe(candidateId);
        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeTrue();
        response.Term.ShouldBe(new Term(1));
    }

    [Fact]
    public async Task GrantsVote_WhenCandidateTermIsHigher_AndLogUpToDate_AndFirstVote()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var state = new State { CurrentTerm = new Term(2) };
        var node = new RaftNode(nodeId, [candidateId], transport, state, new RandomizedElectionTimeout());

        var request = new RequestVoteRequest
        {
            Term = new Term(3),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeTrue();
        response.Term.ShouldBe(new Term(3));
        transport.SendTo.ShouldBe(candidateId);
    }

    [Fact]
    public async Task GrantsVote_WhenAlreadyVotedForSameCandidateInSameTerm()
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
                CurrentTerm = new Term(5),
                VotedFor = candidateId
            },
            new RandomizedElectionTimeout());

        var request = new RequestVoteRequest
        {
            Term = new Term(5),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeTrue();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
    }

    [Fact]
    public async Task RejectsVote_WhenCandidateTermIsLessThanCurrentTerm()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var node = new RaftNode(nodeId, [candidateId], transport, new State { CurrentTerm = new Term(5) },
            new RandomizedElectionTimeout());


        var request = new RequestVoteRequest
        {
            Term = new Term(4),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
    }

    [Fact]
    public async Task RejectsVote_WhenAlreadyVotedForAnotherCandidateInSameTerm()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var alreadyVotedFor = new NodeId(3);
        var transport = new FakeTransport();
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            new State
            {
                CurrentTerm = new Term(5),
                VotedFor = alreadyVotedFor
            },
            new RandomizedElectionTimeout());

        var request = new RequestVoteRequest
        {
            Term = new Term(5),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
    }

    [Fact]
    public async Task RejectsVote_WhenCandidateLogIsOutdated()
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
                CurrentTerm = new Term(5),
                LogEntries = [new LogEntry { Term = new Term(5), Command = "set x = 1" }]
            },
            new RandomizedElectionTimeout());

        var request = new RequestVoteRequest
        {
            Term = new Term(5),
            CandidateId = candidateId,
            LastLogIndex = 0, // Candidate index 0 (less than node index 0)
            LastLogTerm = new Term(4) // Candidate term 4 (less than node's term 5)
        };

        await node.ReceiveAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
    }
}