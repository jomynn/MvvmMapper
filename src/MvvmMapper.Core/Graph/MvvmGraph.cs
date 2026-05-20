namespace MvvmMapper.Core.Graph;

/// <summary>Immutable in-memory graph produced by GraphBuilder.</summary>
public sealed class MvvmGraph
{
    private readonly Dictionary<string, Node> _nodes;
    private readonly List<Edge> _edges;

    internal MvvmGraph(Dictionary<string, Node> nodes, List<Edge> edges)
    {
        _nodes = nodes;
        _edges = edges;
    }

    public IReadOnlyDictionary<string, Node> Nodes => _nodes;
    public IReadOnlyList<Edge> Edges => _edges;

    public IEnumerable<Edge> EdgesFrom(string nodeId) =>
        _edges.Where(e => e.FromId == nodeId);

    public IEnumerable<Edge> EdgesTo(string nodeId) =>
        _edges.Where(e => e.ToId == nodeId);

    public IEnumerable<EndpointNode> ReachableEndpoints(string viewId)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(viewId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            foreach (var edge in EdgesFrom(current))
            {
                queue.Enqueue(edge.ToId);
            }
        }

        return visited
            .Where(id => _nodes.TryGetValue(id, out var n) && n.Kind == NodeKind.Endpoint)
            .Select(id => (EndpointNode)_nodes[id]);
    }
}
