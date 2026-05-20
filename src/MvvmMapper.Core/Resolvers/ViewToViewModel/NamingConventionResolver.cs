using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;

namespace MvvmMapper.Core.Resolvers.ViewToViewModel;

public sealed class NamingConventionResolver : IResolver
{
    private readonly MvvmMapConfig _config;
    private readonly XamlParser _xamlParser;
    private readonly IFileSystem _fs;
    private readonly ILogger<NamingConventionResolver> _logger;

    public NamingConventionResolver(
        MvvmMapConfig config,
        XamlParser xamlParser,
        IFileSystem fs,
        ILogger<NamingConventionResolver> logger)
    {
        _config = config;
        _xamlParser = xamlParser;
        _fs = fs;
        _logger = logger;
    }

    public Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default)
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        // Build lookup: files that match a ViewModel suffix pattern
        var vmFiles = discovery.CsFiles
            .Where(f => _config.Patterns.ViewModelSuffix.Any(s =>
                _fs.GetFileNameWithoutExtension(f).EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var xamlFile in discovery.XamlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var xamlName = _fs.GetFileNameWithoutExtension(xamlFile);

            // Check this file IS a view (its name ends with a View suffix)
            var matchedViewSuffix = _config.Patterns.ViewSuffix.FirstOrDefault(s =>
                xamlName.EndsWith(s, StringComparison.OrdinalIgnoreCase));
            if (matchedViewSuffix is null) continue;

            var baseName = xamlName[..^matchedViewSuffix.Length];
            if (string.IsNullOrEmpty(baseName)) continue;

            foreach (var vmSuffix in _config.Patterns.ViewModelSuffix)
            {
                var expectedVmName = baseName + vmSuffix;
                var vmFile = vmFiles.FirstOrDefault(f =>
                    _fs.GetFileNameWithoutExtension(f).Equals(expectedVmName, StringComparison.OrdinalIgnoreCase));
                if (vmFile is null) continue;

                var doc = _xamlParser.TryParse(xamlFile);
                var viewId = doc is not null ? NodeIds.ForView(doc) : NodeIds.ForViewFile(xamlFile);
                var viewNode = new ViewNode(viewId, xamlName, xamlFile, null, doc?.XClass ?? string.Empty);
                nodes.Add(viewNode);

                var vmId = NodeIds.ForViewModel(expectedVmName);
                var vmNode = new ViewModelNode(vmId, expectedVmName, vmFile, null, expectedVmName);
                nodes.Add(vmNode);

                edges.Add(new Edge(
                    viewId, vmId, EdgeKind.BindsTo, Confidence.Low,
                    $"Naming convention match: {xamlName} ↔ {expectedVmName}"));

                _logger.LogDebug("NamingConvention: {View} → {VM} [Low]", viewId, vmId);
            }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }
}
