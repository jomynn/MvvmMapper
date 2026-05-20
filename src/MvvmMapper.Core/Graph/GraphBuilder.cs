using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Resolvers;

namespace MvvmMapper.Core.Graph;

/// <summary>Orchestrates all resolvers to produce the final MvvmGraph.</summary>
public sealed class GraphBuilder
{
    private readonly IEnumerable<IResolver> _resolvers;
    private readonly ILogger<GraphBuilder> _logger;

    public GraphBuilder(IEnumerable<IResolver> resolvers, ILogger<GraphBuilder> logger)
    {
        _resolvers = resolvers;
        _logger = logger;
    }

    public async Task<MvvmGraph> BuildAsync(
        Discovery.DiscoveryResult discovery,
        CancellationToken cancellationToken = default)
    {
        var nodes = new Dictionary<string, Node>();
        var edges = new List<Edge>();

        foreach (var resolver in _resolvers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await resolver.ResolveAsync(discovery, cancellationToken);
                foreach (var node in result.Nodes)
                    nodes.TryAdd(node.Id, node);
                edges.AddRange(result.Edges);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resolver {Resolver} failed", resolver.GetType().Name);
            }
        }

        return new MvvmGraph(nodes, edges);
    }
}
