using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Resolvers.Endpoints;

public sealed class RestSharpResolver : IResolver
{
    private readonly IFileSystem _fs;
    private readonly ILogger<RestSharpResolver> _logger;

    public RestSharpResolver(IFileSystem fs, ILogger<RestSharpResolver> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default)
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        foreach (var csFile in discovery.CsFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { ProcessFile(csFile, nodes, edges); }
            catch (Exception ex) { _logger.LogError(ex, "RestSharpResolver failed on {File}", csFile); }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }

    private void ProcessFile(string csFile, List<Node> nodes, List<Edge> edges)
    {
        var source = _fs.ReadAllText(csFile);
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        var creations = root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(oc => oc.Type.ToString().EndsWith("RestRequest", StringComparison.OrdinalIgnoreCase));

        foreach (var creation in creations)
        {
            var argList = creation.ArgumentList;
            if (argList is null || argList.Arguments.Count == 0) continue;

            var routeArg = argList.Arguments[0].Expression;
            var (route, confidence) = routeArg is LiteralExpressionSyntax lit
                ? (lit.Token.ValueText, Confidence.High)
                : (routeArg.ToString(), Confidence.Low);

            var verb = "GET"; // default
            if (argList.Arguments.Count >= 2)
            {
                var verbArg = argList.Arguments[1].Expression;
                verb = ExtractVerb(verbArg) ?? "GET";
            }

            var containingMethod = creation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var containingClass = creation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (containingMethod is null || containingClass is null) continue;

            var ns = GetNamespace(containingClass);
            var fqn = ns is not null ? $"{ns}.{containingClass.Identifier.Text}" : containingClass.Identifier.Text;
            var methodName = containingMethod.Identifier.Text;
            var line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            var methodId = NodeIds.ForMethod(fqn, methodName);
            var endpointId = NodeIds.ForEndpoint(verb, route);

            var methodNode = new MethodNode(methodId, methodName, csFile,
                containingMethod.GetLocation().GetLineSpan().StartLinePosition.Line + 1, fqn);
            var endpointNode = new EndpointNode(endpointId, $"{verb} {route}", csFile, line, verb, route);

            nodes.Add(methodNode);
            nodes.Add(endpointNode);
            edges.Add(new Edge(methodId, endpointId, EdgeKind.Hits, confidence,
                $"RestSharp new RestRequest(\"{route}\", Method.{verb}) at line {line}"));

            _logger.LogDebug("RestSharp: {Method} → {Verb} {Route} [{Conf}]", methodId, verb, route, confidence);
        }
    }

    private static string? ExtractVerb(ExpressionSyntax expr)
    {
        if (expr is MemberAccessExpressionSyntax ma)
            return ma.Name.Identifier.Text.ToUpperInvariant();
        return null;
    }

    private static string? GetNamespace(ClassDeclarationSyntax classDecl)
    {
        SyntaxNode? parent = classDecl.Parent;
        while (parent is not null)
        {
            if (parent is FileScopedNamespaceDeclarationSyntax fsns) return fsns.Name.ToString();
            if (parent is NamespaceDeclarationSyntax ns) return ns.Name.ToString();
            parent = parent.Parent;
        }
        return null;
    }
}
