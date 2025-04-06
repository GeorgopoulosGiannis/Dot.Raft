using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dot.Raft.Transport.Http;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRaftHttp(IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<IRaftTransport, HttpClientTransport>();
        serviceCollection.TryAddSingleton<IAddressRetriever, AddressRetriever>();
        return serviceCollection;
    }
}
