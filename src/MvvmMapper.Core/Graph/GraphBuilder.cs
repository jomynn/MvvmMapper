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
        var rawEdges = new List<Edge>();

        foreach (var resolver in _resolvers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await resolver.ResolveAsync(discovery, cancellationToken);
                foreach (var node in result.Nodes)
                    nodes.TryAdd(node.Id, node);
                rawEdges.AddRange(result.Edges);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resolver {Resolver} failed", resolver.GetType().Name);
            }
        }

        var edges = MergeEdges(rawEdges);

        return new MvvmGraph(nodes, edges);
    }

    /// <summary>
    /// Merges duplicate edges (same FromId + ToId + Kind).
    /// Keeps the highest confidence; concatenates all reason strings.
    /// </summary>
    private static List<Edge> MergeEdges(List<Edge> rawEdges)
    {
        var groups = rawEdges.GroupBy(e => (e.FromId, e.ToId, e.Kind));
        var merged = new List<Edge>(rawEdges.Count);

        foreach (var group in groups)
        {
            var entries = group.ToList();
            if (entries.Count == 1)
            {
                merged.Add(entries[0]);
                continue;
            }

            var best = entries.OrderBy(e => e.Confidence).First(); // Low=2, Medium=1, High=0
            var allReasons = entries.Select(e => e.Reason).Distinct(StringComparer.Ordinal).ToList();
            var combinedReason = allReasons.Count == 1
                ? allReasons[0]
                : string.Join("; also: ", allReasons);

            merged.Add(best with { Reason = combinedReason });
        }

        return merged;
    }
}
