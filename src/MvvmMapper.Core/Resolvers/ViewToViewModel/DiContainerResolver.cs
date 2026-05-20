using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;

namespace MvvmMapper.Core.Resolvers.ViewToViewModel;

public sealed class DiContainerResolver : IResolver
{
    private static readonly string[] s_registrationMethods =
        ["AddSingleton", "AddTransient", "AddScoped", "AddScopedAsImplementedInterfaces", "RegisterType", "RegisterSingleton"];

    private readonly MvvmMapConfig _config;
    private readonly XamlParser _xamlParser;
    private readonly IFileSystem _fs;
    private readonly ILogger<DiContainerResolver> _logger;

    public DiContainerResolver(
        MvvmMapConfig config,
        XamlParser xamlParser,
        IFileSystem fs,
        ILogger<DiContainerResolver> logger)
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

        var bootstrapFiles = discovery.CsFiles
            .Where(f => IsBootstrapFile(_fs.GetFileName(f)))
            .ToList();

        if (bootstrapFiles.Count == 0) return Task.FromResult(ResolverResult.Empty);

        var registeredTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in bootstrapFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectRegisteredTypes(file, registeredTypes);
        }

        if (registeredTypes.Count == 0) return Task.FromResult(ResolverResult.Empty);

        // Find Views that take a registered VM in their constructor
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

                var ctors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
                foreach (var ctor in ctors)
                {
                    foreach (var param in ctor.ParameterList.Parameters)
                    {
                        var typeName = param.Type?.ToString();
                        if (typeName is null) continue;
                        if (!registeredTypes.Contains(typeName)) continue;
                        if (!_config.Patterns.ViewModelSuffix.Any(s =>
                            typeName.EndsWith(s, StringComparison.OrdinalIgnoreCase))) continue;

                        var xamlFile = cbFile[..^3]; // strip ".cs" from ".xaml.cs"
                        if (!discovery.XamlFiles.Contains(xamlFile)) continue;

                        var doc = _xamlParser.TryParse(xamlFile);
                        var viewId = doc is not null ? NodeIds.ForView(doc) : NodeIds.ForViewFile(xamlFile);
                        var viewClassName = _fs.GetFileNameWithoutExtension(xamlFile);
                        var viewNode = new ViewNode(viewId, viewClassName, xamlFile, null, doc?.XClass ?? string.Empty);
                        nodes.Add(viewNode);

                        var vmId = NodeIds.ForViewModel(typeName);
                        var vmNode = new ViewModelNode(vmId, typeName, cbFile, null, typeName);
                        nodes.Add(vmNode);

                        edges.Add(new Edge(
                            viewId, vmId, EdgeKind.BindsTo, Confidence.Medium,
                            $"View and ViewModel both registered in DI; View constructor accepts {typeName}"));

                        _logger.LogDebug("DI: {View} → {VM} [Medium]", viewId, vmId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DiContainerResolver failed on {File}", cbFile);
            }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }

    private void CollectRegisteredTypes(string file, HashSet<string> result)
    {
        try
        {
            var source = _fs.ReadAllText(file);
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var root = syntaxTree.GetCompilationUnitRoot();

            var invocations = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var inv in invocations)
            {
                var memberAccess = inv.Expression as MemberAccessExpressionSyntax;
                var name = memberAccess?.Name.Identifier.Text
                    ?? (inv.Expression as GenericNameSyntax)?.Identifier.Text;

                if (name is null) continue;
                if (!s_registrationMethods.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;

                if (memberAccess?.Name is GenericNameSyntax generic)
                {
                    foreach (var typeArg in generic.TypeArgumentList.Arguments)
                        result.Add(typeArg.ToString());
                }
            }
        }
        catch
        {
            // Best effort — swallow silently
        }
    }

    private static bool IsBootstrapFile(string fileName) =>
        fileName.Equals("App.xaml.cs", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("Bootstrapper.cs", StringComparison.OrdinalIgnoreCase);
}
