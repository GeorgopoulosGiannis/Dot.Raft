namespace Dot.Raft;

public class RaftNode
{
    private State State { get; } = new();

    private int _elapsedTicks = 0;
    private int _electionTimeoutTicks;
    private readonly IRaftTransport _transport;
    private readonly IElectionTimeoutProvider _electionTimeoutProvider;
    private readonly List<NodeId> _peers;
    private HashSet<NodeId> _votesReceived = [];


    /// <summary>
    /// Used in unit tests.
    /// </summary>
    internal RaftNode(
        NodeId nodeId,
        List<NodeId> peers,
        IRaftTransport transport,
        State state,
        IElectionTimeoutProvider electionTimeoutProvider,
        RaftRole role = RaftRole.Follower
    ) : this(nodeId, peers, transport, electionTimeoutProvider)
    {
        State = state;
        Role = role;
    }

    /// <summary>
    /// Creates a new <see cref="Raft.RaftNode"/>.
    /// </summary>
    /// <param name="nodeId">The <see cref="NodeId"/> of the current node.</param>
    /// <param name="peers">The peers of the RAFT cluster.</param>
    /// <param name="transport">The <see cref="IRaftTransport"/> to use for sending messages to peers.</param>
    /// <param name="electionTimeoutProvider">The <see cref="IElectionTimeoutProvider"/> to use in order to get election timeout ticks.</param>
    public RaftNode(NodeId nodeId,
        List<NodeId> peers,
        IRaftTransport transport,
        IElectionTimeoutProvider electionTimeoutProvider)
    {
        _transport = transport;
        _electionTimeoutProvider = electionTimeoutProvider;
        _peers = peers;
        Id = nodeId;
        ResetElectionTimeout();
    }

    public NodeId Id { get; }
    public RaftRole Role { get; private set; } = RaftRole.Follower;

    public async Task TickAsync()
    {
        // Logic for election timeout, heartbeats, etc. will go here.
        _elapsedTicks++;

        if (Role is RaftRole.Follower or RaftRole.Candidate && _elapsedTicks >= _electionTimeoutTicks)
        {
            await StartElectionAsync();
        }
    }

    private async Task StartElectionAsync()
    {
        Role = RaftRole.Candidate;
        State.CurrentTerm++;
        State.VotedFor = Id;
        _elapsedTicks = 0;
        _votesReceived = [Id];

        ResetElectionTimeout();

        var lastLogIndex = State.LogEntries.Count - 1;
        var lastLogTerm = State.LogEntries.Count > 0
            ? State.LogEntries[^1].Term
            : new Term(0);

        var request = new RequestVoteRequest
        {
            Term = State.CurrentTerm,
            CandidateId = Id,
            LastLogIndex = lastLogIndex,
            LastLogTerm = lastLogTerm
        };

        foreach (var peer in _peers)
        {
            await _transport.SendAsync(peer, request);
        }
    }

    private void ResetElectionTimeout()
    {
        _electionTimeoutTicks = _electionTimeoutProvider.GetTimeoutTicks();
    }

    public async Task ReceiveAsync(object message)
    {
        var task = message switch
        {
            RequestVoteRequest request => ReceiveAsync(request),
            RequestVoteResponse response => ReceiveAsync(response),
            AppendEntriesRequest request => ReceiveAsync(request),
            _ => Task.CompletedTask,
        };
        await task;
    }

    /// <summary>
    /// Handles a <see cref="RequestVoteRequest"/>.
    /// The receiver will:
    /// 1. Reply false if term &lt; currentTerm
    /// 2. Grant vote,if votedFor is null or candidateId, and candidate's log is at least as up-to-date as receiver's log. 
    /// </summary>
    /// <param name="requestVoteRequest"></param>
    private async Task ReceiveAsync(RequestVoteRequest requestVoteRequest)
    {
        if (requestVoteRequest.Term < State.CurrentTerm)
        {
            await _transport.SendAsync(requestVoteRequest.CandidateId, new RequestVoteResponse
            {
                ReplierId = Id,
                Term = State.CurrentTerm,
                VoteGranted = false,
            });
            return;
        }

        Role = RaftRole.Follower;
        State.CurrentTerm = requestVoteRequest.Term;

        var alreadyVoted = State.VotedFor is not null
                           && State.VotedFor != requestVoteRequest.CandidateId;

        var logUpToDate =
            IsCandidateLogUpToDate(requestVoteRequest.LastLogIndex, requestVoteRequest.LastLogTerm);

        var shouldGrantVote = !alreadyVoted && logUpToDate;

        if (shouldGrantVote)
        {
            State.VotedFor = requestVoteRequest.CandidateId;
        }

        await _transport.SendAsync(requestVoteRequest.CandidateId, new RequestVoteResponse
        {
            ReplierId = Id,
            Term = State.CurrentTerm,
            VoteGranted = shouldGrantVote,
        });
    }

    private bool IsCandidateLogUpToDate(int candidateLastIndex, Term candidateLastTerm)
    {
        if (State.LogEntries.Count == 0)
        {
            return true;
        }

        var lastLocalLogTerm = State.LogEntries[^1].Term;
        var lastLocalLogIndex = State.LogEntries.Count - 1;

        if (candidateLastTerm > lastLocalLogTerm)
        {
            return true;
        }

        if (candidateLastTerm < lastLocalLogTerm)
        {
            return false;
        }

        return candidateLastIndex >= lastLocalLogIndex;
    }

    private async Task ReceiveAsync(RequestVoteResponse response)
    {
        // response term was greater than anything seen, convert to follower
        if (response.Term > State.CurrentTerm)
        {
            _votesReceived.Clear();
            Role = RaftRole.Follower;
            State.CurrentTerm = response.Term;
            State.VotedFor = null;
            return;
        }

        // If old response ignore
        if (Role != RaftRole.Candidate || response.Term < State.CurrentTerm)
        {
            return;
        }

        if (response.VoteGranted)
        {
            _votesReceived.Add(response.ReplierId);
            var majority = (_peers.Count + 1) / 2 + 1;

            if (_votesReceived.Count >= majority)
            {
                Role = RaftRole.Leader;
                // TODO: initialize leader state (nextIndex[], matchIndex[]) later
            }
        }
    }

    private async Task ReceiveAsync(AppendEntriesRequest request)
    {
        if (request.Term < State.CurrentTerm)
        {
            await _transport.SendAsync(request.LeaderId, new AppendEntriesResponse
            {
                Term = State.CurrentTerm,
                Success = false,
            });
            return;
        }

        if (request.Term > State.CurrentTerm)
        {
            Role = RaftRole.Follower;
            State.CurrentTerm = request.Term;
            State.VotedFor = null;
        }

        _elapsedTicks = 0;

        if (request.PrevLogIndex >= State.LogEntries.Count ||
            (request.PrevLogIndex >= 0 &&
             State.LogEntries[request.PrevLogIndex].Term != request.PrevLogTerm))
        {
            await _transport.SendAsync(request.LeaderId, new AppendEntriesResponse
            {
                Term = State.CurrentTerm,
                Success = false,
            });
            return;
        }

        // Remove conflict entries
        var index = request.PrevLogIndex + 1;
        var entryIdx = 0;
        while (index < State.LogEntries.Count && entryIdx < request.Entries.Length)
        {
            if (State.LogEntries[index].Term != request.Entries[entryIdx].Term)
            {
                State.LogEntries.RemoveRange(index, State.LogEntries.Count - index);
                break;
            }

            index++;
            entryIdx++;
        }

        // Append new entries
        for (; entryIdx < request.Entries.Length; entryIdx++)
        {
            State.LogEntries.Add(request.Entries[entryIdx]);
        }

        // Update commit index
        if (request.LeaderCommit > State.CommitIndex)
        {
            State.CommitIndex = Math.Min(request.LeaderCommit, State.LogEntries.Count - 1);
        }

        await _transport.SendAsync(request.LeaderId, new AppendEntriesResponse
        {
            Term = State.CurrentTerm,
            Success = true,
        });
    }

    public Task SubmitCommandAsync(object command)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// The state of a raft node.
/// </summary>
public class State
{
    #region Persisted - The persistent state of a Raft node. It is updated on stable storage before responding to RPCs.

    /// <summary>
    /// Latest term server has seen (initialized to 0 on first boot, increases monotonically).
    /// </summary>
    public Term CurrentTerm { get; set; } = default;

    /// <summary>
    /// CandidateId that received vote in current term (or null if none).
    /// </summary>
    public NodeId? VotedFor { get; set; }

    /// <summary>
    /// Log entries; Each entry contains command for state machine,
    /// and term when entry was received by leader (first index is 1). 
    /// </summary>
    public List<LogEntry> LogEntries { get; init; } = [];

    #endregion


    #region Volatile - Volatile state on nodes. Reinitialized after election.

    /// <summary>
    /// Index of the highest log entry known to be commited.
    /// Initialized to 0, increases monotonically.
    /// </summary>
    public int CommitIndex { get; set; } = 0;

    /// <summary>
    /// Index of the highest log entry applied to state machine.
    /// Initialized to 0, increases monotonically.
    /// </summary>
    public int LastApplied { get; set; } = 0;

    #endregion

    #region Leader Volatile

    /// <summary>
    /// For each server, index of the next log entry to send to that server.
    /// Initialized to leader last log index + 1.
    /// </summary>
    public int[] NextIndexes { get; init; } = [];

    /// <summary>
    /// For each server, index of highest log entry known to be replicated on server.
    /// Initialized to 0, increases monotonically.
    /// </summary>
    public int[] MatchIndexes { get; init; } = [];

    #endregion
}