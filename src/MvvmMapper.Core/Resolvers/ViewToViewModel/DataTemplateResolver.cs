using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;

namespace MvvmMapper.Core.Resolvers.ViewToViewModel;

public sealed class DataTemplateResolver : IResolver
{
    private readonly XamlParser _xamlParser;
    private readonly ILogger<DataTemplateResolver> _logger;

    public DataTemplateResolver(XamlParser xamlParser, ILogger<DataTemplateResolver> logger)
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
            if (doc is null || doc.DataTemplates.Count == 0) continue;

            foreach (var dt in doc.DataTemplates)
            {
                if (string.IsNullOrEmpty(dt.ViewModelTypeName)) continue;

                var vmFqn = dt.ViewModelClrNamespace is not null
                    ? $"{dt.ViewModelClrNamespace}.{dt.ViewModelTypeName}"
                    : dt.ViewModelTypeName;

                var vmId = NodeIds.ForViewModel(vmFqn);
                var vmNode = new ViewModelNode(vmId, dt.ViewModelTypeName, xamlFile, dt.LineNumber, vmFqn);
                nodes.Add(vmNode);

                if (!string.IsNullOrEmpty(dt.ViewTypeName))
                {
                    var viewFqn = dt.ViewClrNamespace is not null
                        ? $"{dt.ViewClrNamespace}.{dt.ViewTypeName}"
                        : dt.ViewTypeName;

                    var viewId = $"view:{viewFqn}";
                    var viewNode = new ViewNode(viewId, dt.ViewTypeName, xamlFile, dt.LineNumber, viewFqn);
                    nodes.Add(viewNode);

                    edges.Add(new Edge(
                        viewId, vmId, EdgeKind.BindsTo, Confidence.High,
                        $"DataTemplate with DataType={{x:Type vm:{dt.ViewModelTypeName}}}"));

                    _logger.LogDebug("DataTemplate: {View} → {VM} [High]", viewId, vmId);
                }
                else
                {
                    _logger.LogDebug("DataTemplate: {VM} implicitly bound via DataTemplate (no child view element)", vmId);
                }
            }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }
}
