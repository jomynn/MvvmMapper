using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;

namespace MvvmMapper.Core.Resolvers.ViewToViewModel;

public sealed class ExplicitXamlResolver : IResolver
{
    private readonly XamlParser _xamlParser;
    private readonly ILogger<ExplicitXamlResolver> _logger;

    public ExplicitXamlResolver(XamlParser xamlParser, ILogger<ExplicitXamlResolver> logger)
    {
        _xamlParser = xamlParser;
        _logger = logger;
    }

    public Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default)
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        foreach (var xamlFile in discovery.XamlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = _xamlParser.TryParse(xamlFile);
            if (doc is null) continue;

            var explicitDcs = doc.DataContexts
                .Where(dc => dc.Kind == DataContextKind.ExplicitElement)
                .ToList();

            if (explicitDcs.Count == 0) continue;

            var viewId = NodeIds.ForView(doc);
            var viewNode = new ViewNode(viewId, doc.XClass ?? xamlFile, xamlFile, null, doc.XClass ?? string.Empty);
            nodes.Add(viewNode);

            foreach (var dc in explicitDcs)
            {
                var fqn = dc.ClrNamespace != null ? $"{dc.ClrNamespace}.{dc.TypeName}" : dc.TypeName;
                var vmId = NodeIds.ForViewModel(fqn);
                var vmNode = new ViewModelNode(vmId, dc.TypeName, xamlFile, dc.LineNumber, fqn);
                nodes.Add(vmNode);

                edges.Add(new Edge(
                    viewId, vmId, EdgeKind.BindsTo, Confidence.High,
                    $"Explicit <{doc.RootElementType}.DataContext> element in XAML"));

                _logger.LogDebug("ExplicitXaml: {View} → {VM} [High]", viewId, vmId);
            }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }
}
