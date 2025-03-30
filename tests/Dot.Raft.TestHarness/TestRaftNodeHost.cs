namespace Dot.Raft.TestHarness;

public class TestRaftNodeHost(IRaftNode node)
{
    public IRaftNode Node { get; } = node;
    private readonly Queue<object> _inbox = new();

    public void EnqueueMessage(object message)
    {
        _inbox.Enqueue(message);
    }

    public async Task TickAsync()
    {
        await Node.TickAsync();

        while (_inbox.Count > 0)
        {
            var msg = _inbox.Dequeue();
            // In a real setup we might track sender; for now assume from any node
            await Node.ReceivePeerMessageAsync(msg);
        }
    }
}