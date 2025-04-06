using System.Net.Http.Json;

namespace Dot.Raft.Transport.Http;

public class HttpClientTransport(
    IAddressRetriever addressRetriever,
    HttpClient httpClient) : IRaftTransport
{
    public async Task SendAsync(NodeId sendTo, object command)
    {
        var uri = addressRetriever.GetAddress(sendTo);
        httpClient.BaseAddress = uri;
        var endpoint = command switch
        {
            RequestVote => "/dotRaft/requestVote",
            AppendEntries => "/dotRaft/appendEntries",
            RequestVoteResponse => "/dotRaft/requestVoteResponse",
            AppendEntriesResponse => "/dotRaft/appendEntriesResponse",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown Command")
        };
        await httpClient.PostAsync(endpoint, JsonContent.Create(command));
    }
}
