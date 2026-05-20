using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;

namespace MvvmMapper.Core.Resolvers.ViewToViewModel;

public sealed class CodeBehindResolver : IResolver
{
    private readonly IFileSystem _fs;
    private readonly XamlParser _xamlParser;
    private readonly ILogger<CodeBehindResolver> _logger;

    public CodeBehindResolver(IFileSystem fs, XamlParser xamlParser, ILogger<CodeBehindResolver> logger)
    {
        _fs = fs;
        _xamlParser = xamlParser;
        _logger = logger;
    }

    public Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default)
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        // code-behind files are .xaml.cs files
        var codeBehindFiles = discovery.CsFiles
            .Where(f => f.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var cbFile in codeBehindFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = _fs.ReadAllText(cbFile);
                var syntaxTree = CSharpSyntaxTree.ParseText(source);
                var root = syntaxTree.GetCompilationUnitRoot();

                // Find assignments to DataContext
                var assignments = root.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(a =>
                    {
                        var left = a.Left.ToString().Trim();
                        return left == "DataContext" || left == "this.DataContext";
                    })
                    .ToList();

                // Find constructor parameters typed as *ViewModel or *VM
                var ctorParams = root.DescendantNodes()
                    .OfType<ConstructorDeclarationSyntax>()
                    .SelectMany(c => c.ParameterList.Parameters)
                    .Where(p => p.Type?.ToString().EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase) == true
                             || p.Type?.ToString().EndsWith("VM", StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                if (assignments.Count == 0 && ctorParams.Count == 0) continue;

                // Corresponding XAML file (strip the ".cs" from ".xaml.cs")
                var xamlFile = cbFile[..^3];
                if (!discovery.XamlFiles.Contains(xamlFile)) continue;

                var doc = _xamlParser.TryParse(xamlFile);
                var viewId = doc is not null ? NodeIds.ForView(doc) : NodeIds.ForViewFile(xamlFile);
                var viewClassName = _fs.GetFileNameWithoutExtension(xamlFile);
                var viewNode = new ViewNode(viewId, viewClassName, xamlFile, null, doc?.XClass ?? string.Empty);
                nodes.Add(viewNode);

                // Track which VMs we've already emitted edges for, to avoid duplicates from both loops
                var emittedVmIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (var assignment in assignments)
                {
                    var right = assignment.Right;
                    string? vmTypeName = null;

                    if (right is ObjectCreationExpressionSyntax objCreation)
                    {
                        vmTypeName = objCreation.Type.ToString();
                    }
                    else if (right is IdentifierNameSyntax identifier)
                    {
                        // DataContext = vm; — look for the type from ctor params
                        var paramMatch = ctorParams.FirstOrDefault(p =>
                            p.Identifier.Text.Equals(identifier.Identifier.Text, StringComparison.Ordinal));
                        if (paramMatch != null)
                            vmTypeName = paramMatch.Type?.ToString();
                    }

                    if (vmTypeName is null) continue;

                    var line = assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var vmId = NodeIds.ForViewModel(vmTypeName);

                    if (emittedVmIds.Add(vmId))
                    {
                        var vmNode = new ViewModelNode(vmId, vmTypeName, cbFile, line, vmTypeName);
                        nodes.Add(vmNode);
                    }

                    edges.Add(new Edge(
                        viewId, vmId, EdgeKind.BindsTo, Confidence.High,
                        "DataContext assigned in code-behind constructor"));

                    _logger.LogDebug("CodeBehind: {View} → {VM} [High]", viewId, vmId);
                }

                // Constructor injection — emit edges for any ViewModel-typed ctor params not already handled
                foreach (var param in ctorParams)
                {
                    var vmTypeName = param.Type!.ToString();
                    var vmId = NodeIds.ForViewModel(vmTypeName);

                    if (emittedVmIds.Add(vmId))
                    {
                        var line = param.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        var vmNode = new ViewModelNode(vmId, vmTypeName, cbFile, line, vmTypeName);
                        nodes.Add(vmNode);

                        edges.Add(new Edge(
                            viewId, vmId, EdgeKind.BindsTo, Confidence.High,
                            "ViewModel injected into View constructor (DataContext = vm)"));

                        _logger.LogDebug("CodeBehind (ctor inject): {View} → {VM} [High]", viewId, vmId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CodeBehindResolver failed on {File}", cbFile);
            }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }
}
