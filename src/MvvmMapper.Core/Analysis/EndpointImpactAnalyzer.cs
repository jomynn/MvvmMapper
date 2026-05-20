using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Analysis;

public sealed class EndpointImpactAnalyzer
{
    public IReadOnlyList<EndpointImpact> Analyze(MvvmGraph graph)
    {
        // Build reverse adjacency for fast lookup
        var reverseEdges = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            if (!reverseEdges.TryGetValue(edge.ToId, out var list))
            {
                list = [];
                reverseEdges[edge.ToId] = list;
            }
            list.Add(edge);
        }

        var results = new List<EndpointImpact>();

        foreach (var node in graph.Nodes.Values.OfType<EndpointNode>())
        {
            var reachableViews = FindReachableViews(node.Id, graph, reverseEdges);
            results.Add(new EndpointImpact(node.Id, node.Verb, node.Route, reachableViews));
        }

        return results;
    }

    private static IReadOnlyList<string> FindReachableViews(
        string endpointId,
        MvvmGraph graph,
        Dictionary<string, List<Edge>> reverseEdges)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var viewIds = new List<string>();
        var queue = new Queue<string>();
        queue.Enqueue(endpointId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            if (graph.Nodes.TryGetValue(current, out var node) && node.Kind == NodeKind.View)
                viewIds.Add(current);

            if (!reverseEdges.TryGetValue(current, out var inbound)) continue;

            foreach (var edge in inbound)
            {
                if (!visited.Contains(edge.FromId))
                    queue.Enqueue(edge.FromId);
            }
        }

        return viewIds;
    }
}
