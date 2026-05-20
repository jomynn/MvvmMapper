using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;

namespace MvvmMapper.Core.Resolvers.ViewToViewModel;

public sealed class LocatorResolver : IResolver
{
    private readonly MvvmMapConfig _config;
    private readonly XamlParser _xamlParser;
    private readonly IFileSystem _fs;
    private readonly ILogger<LocatorResolver> _logger;

    public LocatorResolver(
        MvvmMapConfig config,
        XamlParser xamlParser,
        IFileSystem fs,
        ILogger<LocatorResolver> logger)
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

        // Build locator property map: propertyName → returnTypeName
        var locatorMap = BuildLocatorPropertyMap(discovery.CsFiles, cancellationToken);

        foreach (var xamlFile in discovery.XamlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = _xamlParser.TryParse(xamlFile);
            if (doc is null) continue;

            var locatorBindings = doc.DataContexts
                .Where(dc => dc.Kind == DataContextKind.LocatorBinding)
                .ToList();

            if (locatorBindings.Count == 0) continue;

            var viewId = NodeIds.ForView(doc);
            var viewNode = new ViewNode(viewId, doc.XClass ?? xamlFile, xamlFile, null, doc.XClass ?? string.Empty);
            nodes.Add(viewNode);

            foreach (var binding in locatorBindings)
            {
                if (locatorMap.TryGetValue(binding.TypeName, out var vmTypeName))
                {
                    var vmId = NodeIds.ForViewModel(vmTypeName);
                    var vmNode = new ViewModelNode(vmId, vmTypeName, xamlFile, binding.LineNumber, vmTypeName);
                    nodes.Add(vmNode);

                    edges.Add(new Edge(
                        viewId, vmId, EdgeKind.BindsTo, Confidence.High,
                        $"Locator binding: {{Binding {binding.TypeName}, Source={{StaticResource Locator}}}} resolved via ViewModelLocator.{binding.TypeName}"));

                    _logger.LogDebug("Locator: {View} → {VM} [High]", viewId, vmId);
                }
                else
                {
                    // Locator detected but property unresolved → Low confidence
                    var vmId = NodeIds.ForViewModel(binding.TypeName);
                    var vmNode = new ViewModelNode(vmId, binding.TypeName, xamlFile, binding.LineNumber, binding.TypeName);
                    nodes.Add(vmNode);

                    edges.Add(new Edge(
                        viewId, vmId, EdgeKind.BindsTo, Confidence.Low,
                        $"Locator pattern detected but ViewModelLocator.{binding.TypeName} could not be resolved"));

                    _logger.LogDebug("Locator (unresolved): {View} → {VM} [Low]", viewId, vmId);
                }
            }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }

    private Dictionary<string, string> BuildLocatorPropertyMap(
        IReadOnlyList<string> csFiles,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var csFile in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = _fs.GetFileNameWithoutExtension(csFile);
            if (!_config.LocatorClasses.Contains(fileName, StringComparer.OrdinalIgnoreCase)) continue;

            try
            {
                var source = _fs.ReadAllText(csFile);
                var syntaxTree = CSharpSyntaxTree.ParseText(source);
                var root = syntaxTree.GetCompilationUnitRoot();

                var properties = root.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .ToList();

                foreach (var prop in properties)
                {
                    var typeName = prop.Type.ToString();
                    map[prop.Identifier.Text] = typeName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LocatorResolver failed parsing {File}", csFile);
            }
        }

        return map;
    }
}
