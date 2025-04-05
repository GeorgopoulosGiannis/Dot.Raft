namespace Dot.Raft;

/// <summary>
/// Represents a client-submitted command with metadata for deduplication and idempotent execution.
/// </summary>
public record ClientCommandEnvelope
{
    /// <summary>
    /// Gets the unique identifier of the client submitting the command.
    /// This is used to track per-client command history and ensure idempotency.
    /// </summary>
    public string ClientId { get; init; }

    /// <summary>
    /// Gets the sequence number of this command from the client.
    /// The state machine will use this to detect and suppress duplicate executions.
    /// </summary>
    public int SequenceNumber { get; init; }

    /// <summary>
    /// Gets the actual command to be replicated and applied to the state machine.
    /// This should be a domain-specific operation such as "Put", "Delete", etc.
    /// </summary>
    public object Command { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientCommandEnvelope"/> record.
    /// </summary>
    /// <param name="clientId">The unique client ID.</param>
    /// <param name="sequenceNumber">The command's sequence number for this client.</param>
    /// <param name="command">The actual command payload.</param>
    public ClientCommandEnvelope(string clientId, int sequenceNumber, object command)
    {
        ClientId = clientId;
        SequenceNumber = sequenceNumber;
        Command = command;
    }
}
