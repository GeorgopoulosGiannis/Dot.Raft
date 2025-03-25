namespace Dot.Raft;

/// <summary>
/// Interface for sending messages to other Raft nodes.
/// </summary>
public interface IRaftTransport
{
    Task SendAsync<T>(NodeId sendTo, T command);
}