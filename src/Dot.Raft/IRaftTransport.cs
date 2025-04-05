namespace Dot.Raft;

/// <summary>
/// Interface for sending messages to other Raft nodes.
/// </summary>
public interface IRaftTransport
{
    /// <summary>
    /// Call to send the <paramref name="command"/> to the specified <paramref name="sendTo"/> node.
    /// </summary>
    /// <param name="sendTo">The <see cref="NodeId"/> to send the command to.</param>
    /// <param name="command">The command to send.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendAsync(NodeId sendTo, object command);
}
