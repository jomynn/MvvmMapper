using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Analysis;

public sealed class FanOutAnalyzer
{
    public IReadOnlyList<SharedViewModelInfo> Analyze(MvvmGraph graph)
    {
        var bindingEdges = graph.Edges.Where(e => e.Kind == EdgeKind.BindsTo).ToList();

        return bindingEdges
            .GroupBy(e => e.ToId, StringComparer.Ordinal)
            .Where(g => g.Count() >= 2)
            .Select(g =>
            {
                var vmId = g.Key;
                graph.Nodes.TryGetValue(vmId, out var vmNode);
                return new SharedViewModelInfo(
                    vmId,
                    vmNode?.DisplayName ?? vmId,
                    g.Count(),
                    g.Select(e => e.FromId).Distinct().ToList());
            })
            .OrderByDescending(s => s.FanIn)
            .ToList();
    }
}
