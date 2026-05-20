using System.Text;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Rendering;

public sealed class MermaidRenderer(ILogger<MermaidRenderer> logger) : IRenderer
{
    public string Format => "mermaid";

    public async Task RenderAsync(MvvmGraph graph, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        await WriteByViewAsync(graph, outputDirectory, cancellationToken);
        await WriteByVmAsync(graph, outputDirectory, cancellationToken);
        await WriteByEndpointAsync(graph, outputDirectory, cancellationToken);

        logger.LogInformation("Wrote Mermaid files to {Dir}", outputDirectory);
    }

    private static async Task WriteByViewAsync(MvvmGraph graph, string outputDirectory, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MVVM Map — By View");
        sb.AppendLine();

        var views = graph.Nodes.Values.OfType<ViewNode>().OrderBy(v => v.DisplayName);

        foreach (var view in views)
        {
            sb.AppendLine($"## {view.DisplayName}");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("flowchart LR");

            var vId = SafeId(view.Id);
            var vLabel = SafeLabel(view.DisplayName);
            sb.AppendLine($"    {vId}[\"{vLabel}\"]");

            // BindsTo edges from this view
            var bindEdges = graph.EdgesFrom(view.Id)
                .Where(e => e.Kind == EdgeKind.BindsTo)
                .ToList();

            foreach (var edge in bindEdges)
            {
                if (!graph.Nodes.TryGetValue(edge.ToId, out var vmNode)) continue;
                var vmId = SafeId(vmNode.Id);
                var vmLabel = SafeLabel(vmNode.DisplayName);
                sb.AppendLine($"    {vmId}[\"{vmLabel}\"]");
                sb.AppendLine($"    {vId} -->|\"{edge.Kind} {edge.Confidence}\"| {vmId}");

                // Endpoints reachable from this view
                foreach (var endpoint in graph.ReachableEndpoints(view.Id))
                {
                    var epId = SafeId(endpoint.Id);
                    var epLabel = SafeLabel($"{endpoint.Verb} {endpoint.Route}");
                    sb.AppendLine($"    {epId}[\"{epLabel}\"]");
                    sb.AppendLine($"    {vmId} -->|\"Hits\"| {epId}");
                }
            }

            if (!bindEdges.Any())
            {
                sb.AppendLine($"    {vId}");
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        var path = Path.Combine(outputDirectory, "mermaid-by-view.md");
        await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken);
    }

    private static async Task WriteByVmAsync(MvvmGraph graph, string outputDirectory, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MVVM Map — By ViewModel");
        sb.AppendLine();

        var vms = graph.Nodes.Values.OfType<ViewModelNode>().OrderBy(v => v.DisplayName);

        foreach (var vm in vms)
        {
            var bindingViews = graph.EdgesTo(vm.Id)
                .Where(e => e.Kind == EdgeKind.BindsTo)
                .Select(e => graph.Nodes.TryGetValue(e.FromId, out var n) ? n : null)
                .OfType<Node>()
                .ToList();

            var fanIn = bindingViews.Count;
            var isShared = fanIn >= 2;
            var sharedLabel = isShared ? " [SHARED]" : "";

            sb.AppendLine($"## {vm.DisplayName}{sharedLabel}");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("flowchart LR");

            var vmId = SafeId(vm.Id);
            var vmLabel = SafeLabel(vm.DisplayName + sharedLabel);
            sb.AppendLine($"    {vmId}[\"{vmLabel}\"]");

            foreach (var view in bindingViews)
            {
                var vId = SafeId(view.Id);
                var vLabel = SafeLabel(view.DisplayName);
                sb.AppendLine($"    {vId}[\"{vLabel}\"]");
                sb.AppendLine($"    {vId} -->|\"BindsTo\"| {vmId}");
            }

            foreach (var endpoint in graph.ReachableEndpoints(vm.Id))
            {
                var epId = SafeId(endpoint.Id);
                var epLabel = SafeLabel($"{endpoint.Verb} {endpoint.Route}");
                sb.AppendLine($"    {epId}[\"{epLabel}\"]");
                sb.AppendLine($"    {vmId} -->|\"Hits\"| {epId}");
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        var path = Path.Combine(outputDirectory, "mermaid-by-vm.md");
        await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken);
    }

    private static async Task WriteByEndpointAsync(MvvmGraph graph, string outputDirectory, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MVVM Map — By Endpoint");
        sb.AppendLine();

        var endpoints = graph.Nodes.Values.OfType<EndpointNode>().OrderBy(e => e.Verb).ThenBy(e => e.Route);

        foreach (var endpoint in endpoints)
        {
            sb.AppendLine($"## {endpoint.Verb} {endpoint.Route}");
            sb.AppendLine();
            sb.AppendLine("```mermaid");
            sb.AppendLine("flowchart RL");

            var epId = SafeId(endpoint.Id);
            var epLabel = SafeLabel($"{endpoint.Verb} {endpoint.Route}");
            sb.AppendLine($"    {epId}[\"{epLabel}\"]");

            // Find views that can reach this endpoint via reverse BFS
            var reachingViews = FindReachingViews(graph, endpoint.Id);
            foreach (var view in reachingViews)
            {
                var vId = SafeId(view.Id);
                var vLabel = SafeLabel(view.DisplayName);
                sb.AppendLine($"    {vId}[\"{vLabel}\"]");
                sb.AppendLine($"    {vId} -->|\"reaches\"| {epId}");
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        var path = Path.Combine(outputDirectory, "mermaid-by-endpoint.md");
        await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken);
    }

    private static IEnumerable<ViewNode> FindReachingViews(MvvmGraph graph, string endpointId)
    {
        // Find all methods that hit this endpoint
        var methods = graph.EdgesTo(endpointId)
            .Where(e => e.Kind == EdgeKind.Hits)
            .Select(e => e.FromId)
            .ToHashSet();

        // Find views that either Invokes those methods, or BindsTo a VM that Contains those methods
        var result = new HashSet<ViewNode>();
        foreach (var view in graph.Nodes.Values.OfType<ViewNode>())
        {
            if (graph.ReachableEndpoints(view.Id).Any(ep => ep.Id == endpointId))
            {
                result.Add(view);
            }
        }
        return result.OrderBy(v => v.DisplayName);
    }

    private static string SafeId(string id)
    {
        // Create a short safe alphanumeric ID for Mermaid
        var clean = new StringBuilder();
        foreach (var c in id)
        {
            if (char.IsLetterOrDigit(c)) clean.Append(c);
            else clean.Append('_');
        }
        return clean.ToString();
    }

    private static string SafeLabel(string label) =>
        label.Replace('"', '\'').Replace('[', '(').Replace(']', ')');
}
