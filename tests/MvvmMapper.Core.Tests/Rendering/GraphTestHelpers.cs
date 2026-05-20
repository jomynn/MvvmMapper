using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Tests.Rendering;

internal static class GraphTestHelpers
{
    public static MvvmGraph BuildGraph(Dictionary<string, Node> nodes, List<Edge> edges)
    {
        return new MvvmGraph(nodes, edges);
    }
}
