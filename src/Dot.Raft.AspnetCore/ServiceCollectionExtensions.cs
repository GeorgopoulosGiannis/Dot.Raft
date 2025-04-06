using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dot.Raft.AspnetCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRaft(this IServiceCollection services, Action<RaftOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Default timer implementations (can be overridden)
        services.TryAddSingleton<IElectionTimer>(
            _ => new TimeBasedElectionTimer(
                TimeSpan.FromMilliseconds(150),
                TimeSpan.FromMilliseconds(300)
            )
        );

        services.TryAddSingleton<IHeartbeatTimer>(
            _ => new TimeBasedHeartbeatTimer(
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(100)
            )
        );

        services.AddSingleton<IRaftNode>(
            s =>
            {
                var options = s.GetRequiredService<IOptions<RaftOptions>>().Value;
                var transport = s.GetRequiredService<IRaftTransport>();
                var electionTimer = s.GetRequiredService<IElectionTimer>();
                var heartbeatTimer = s.GetRequiredService<IHeartbeatTimer>();

                if (s.GetService<IStateMachine>() is not { } stateMachine)
                {
                    throw new InvalidOperationException(
                        "No IStateMachine registered. Please register your implementation, e.g., services.AddSingleton<IStateMachine, MyStateMachine>();"
                    );
                }

                return new RaftNode(
                    new NodeId(options.NodeId),
                    [],
                    transport,
                    electionTimer,
                    heartbeatTimer,
                    stateMachine
                );
            });

        return services;
    }

    public static IServiceCollection AddRaft<TStateMachine>(
        this IServiceCollection services,
        Action<RaftOptions> configureOptions
    ) where TStateMachine : class, IStateMachine
    {
        services.AddSingleton<IStateMachine, TStateMachine>();
        return services.AddRaft(configureOptions);
    }
}
