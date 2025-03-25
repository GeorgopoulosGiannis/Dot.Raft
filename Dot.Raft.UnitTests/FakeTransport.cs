namespace Dot.Raft.UnitTests;

public class FakeTransport : IRaftTransport
{
    public NodeId? SendTo { get; private set; }
    public object? Command { get; private set; }

    public Task SendAsync<T>(NodeId sendTo, T command)
    {
        SendTo = sendTo;
        Command = command;
        return Task.CompletedTask;
    }
}