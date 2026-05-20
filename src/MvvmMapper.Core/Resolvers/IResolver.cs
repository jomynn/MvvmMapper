using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Resolvers;

public interface IResolver
{
    Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default);
}

public record ResolverResult(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges)
{
    public static ResolverResult Empty { get; } = new([], []);
}
