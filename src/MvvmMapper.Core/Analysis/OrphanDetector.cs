using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Analysis;

public sealed class OrphanDetector
{
    public (IReadOnlyList<OrphanedNode> views, IReadOnlyList<OrphanedNode> vms, IReadOnlyList<OrphanedNode> endpoints)
        Detect(MvvmGraph graph)
    {
        // Views that have no outbound BindsTo edge
        var viewsWithVm = graph.Edges
            .Where(e => e.Kind == EdgeKind.BindsTo)
            .Select(e => e.FromId)
            .ToHashSet(StringComparer.Ordinal);

        var orphanViews = graph.Nodes.Values
            .Where(n => n.Kind == NodeKind.View && !viewsWithVm.Contains(n.Id))
            .Select(n => new OrphanedNode(n.Id, n.DisplayName, "View"))
            .ToList();

        // VMs that have no inbound BindsTo edge
        var vmsWithView = graph.Edges
            .Where(e => e.Kind == EdgeKind.BindsTo)
            .Select(e => e.ToId)
            .ToHashSet(StringComparer.Ordinal);

        var orphanVMs = graph.Nodes.Values
            .Where(n => n.Kind == NodeKind.ViewModel && !vmsWithView.Contains(n.Id))
            .Select(n => new OrphanedNode(n.Id, n.DisplayName, "ViewModel"))
            .ToList();

        // Endpoints that no method Hits (inbound Hits edge count == 0)
        var endpointsWithHits = graph.Edges
            .Where(e => e.Kind == EdgeKind.Hits)
            .Select(e => e.ToId)
            .ToHashSet(StringComparer.Ordinal);

        var orphanEndpoints = graph.Nodes.Values
            .Where(n => n.Kind == NodeKind.Endpoint && !endpointsWithHits.Contains(n.Id))
            .Select(n => new OrphanedNode(n.Id, n.DisplayName, "Endpoint"))
            .ToList();

        return (orphanViews, orphanVMs, orphanEndpoints);
    }
}
