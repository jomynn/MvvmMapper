using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Resolvers.Endpoints;

public sealed class HttpClientResolver : IResolver
{
    private static readonly HashSet<string> s_httpMethods = new(StringComparer.OrdinalIgnoreCase)
        { "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "PatchAsync", "HeadAsync" };

    private readonly IFileSystem _fs;
    private readonly ILogger<HttpClientResolver> _logger;

    public HttpClientResolver(IFileSystem fs, ILogger<HttpClientResolver> logger)
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
            catch (Exception ex) { _logger.LogError(ex, "HttpClientResolver failed on {File}", csFile); }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }

    private void ProcessFile(string csFile, List<Node> nodes, List<Edge> edges)
    {
        var source = _fs.ReadAllText(csFile);
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var inv in invocations)
        {
            string? verb = null;
            ExpressionSyntax? urlArg = null;

            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                var methodName = ma.Name.Identifier.Text;

                if (s_httpMethods.Contains(methodName))
                {
                    verb = methodName.Replace("Async", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
                    urlArg = inv.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                }
                else if (methodName == "SendAsync")
                {
                    // SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/foo"))
                    var msgArg = inv.ArgumentList.Arguments.FirstOrDefault()?.Expression
                        as ObjectCreationExpressionSyntax;
                    if (msgArg != null)
                    {
                        var verbArg = msgArg.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                        var routeArg = msgArg.ArgumentList?.Arguments.ElementAtOrDefault(1)?.Expression;
                        if (verbArg != null)
                            verb = ExtractHttpMethodVerb(verbArg);
                        urlArg = routeArg;
                    }
                }
            }

            if (verb is null || urlArg is null) continue;

            var (route, confidence) = ExtractRoute(urlArg);
            if (route is null) continue;

            var containingMethod = inv.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var containingClass = inv.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (containingMethod is null || containingClass is null) continue;

            var ns = GetNamespace(containingClass);
            var fqn = ns is not null ? $"{ns}.{containingClass.Identifier.Text}" : containingClass.Identifier.Text;
            var methodName2 = containingMethod.Identifier.Text;
            var line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            var methodId = NodeIds.ForMethod(fqn, methodName2);
            var methodNode = new MethodNode(methodId, methodName2, csFile,
                containingMethod.GetLocation().GetLineSpan().StartLinePosition.Line + 1, fqn);

            var endpointId = NodeIds.ForEndpoint(verb, route);
            var endpointNode = new EndpointNode(endpointId, $"{verb} {route}", csFile, line, verb, route);

            nodes.Add(methodNode);
            nodes.Add(endpointNode);
            edges.Add(new Edge(methodId, endpointId, EdgeKind.Hits, confidence,
                $"HttpClient.{verb.ToLowerInvariant()}Async(\"{route}\") at line {line}"));

            _logger.LogDebug("HttpClient: {Method} → {Verb} {Route} [{Conf}]", methodId, verb, route, confidence);
        }
    }

    private static (string? route, Confidence confidence) ExtractRoute(ExpressionSyntax expr) => expr switch
    {
        LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression)
            => (lit.Token.ValueText, Confidence.High),
        InterpolatedStringExpressionSyntax interp => ExtractInterpolatedRoute(interp),
        _ => (null, Confidence.Low)
    };

    private static (string? route, Confidence confidence) ExtractInterpolatedRoute(
        InterpolatedStringExpressionSyntax interp)
    {
        var parts = interp.Contents;
        var sb = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            if (part is InterpolatedStringTextSyntax text)
                sb.Append(text.TextToken.ValueText);
            else
                sb.Append("{?}");
        }
        var route = sb.ToString();
        return string.IsNullOrWhiteSpace(route) ? (null, Confidence.Low) : (route, Confidence.Medium);
    }

    private static string? ExtractHttpMethodVerb(ExpressionSyntax expr)
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
