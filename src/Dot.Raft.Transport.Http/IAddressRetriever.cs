namespace Dot.Raft.Transport.Http;

public interface IAddressRetriever
{
    public Uri GetAddress(NodeId nodeId);
}
