namespace Dot.Raft.Transport.Http;

public class AddressRetriever(IDictionary<NodeId, Uri> addresses) : IAddressRetriever
{
    public Uri GetAddress(NodeId nodeId)
    {
        return addresses[nodeId];
    }
}
