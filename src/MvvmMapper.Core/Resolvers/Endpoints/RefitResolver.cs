using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Resolvers.Endpoints;

public sealed class RefitResolver : IResolver
{
    private static readonly HashSet<string> s_refitVerbs = new(StringComparer.OrdinalIgnoreCase)
        { "Get", "Post", "Put", "Delete", "Patch", "Head", "Options" };

    private readonly IFileSystem _fs;
    private readonly ILogger<RefitResolver> _logger;

    public RefitResolver(IFileSystem fs, ILogger<RefitResolver> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default)
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        // Pass 1: find all Refit interface methods → endpoints
        // interfaceName → list of (methodName, verb, route)
        var refitInterfaces = new Dictionary<string, List<(string methodName, string verb, string route)>>(
            StringComparer.Ordinal);

        foreach (var csFile in discovery.CsFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { ScanRefitInterfaces(csFile, nodes, edges, refitInterfaces); }
            catch (Exception ex) { _logger.LogError(ex, "RefitResolver interface scan failed on {File}", csFile); }
        }

        // Pass 2: find callers of Refit interface methods in classes
        foreach (var csFile in discovery.CsFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { ScanCallers(csFile, nodes, edges, refitInterfaces); }
            catch (Exception ex) { _logger.LogError(ex, "RefitResolver caller scan failed on {File}", csFile); }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }

    private void ScanRefitInterfaces(
        string csFile,
        List<Node> nodes,
        List<Edge> edges,
        Dictionary<string, List<(string, string, string)>> refitInterfaces)
    {
        var source = _fs.ReadAllText(csFile);
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        foreach (var iface in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            var ifaceName = iface.Identifier.Text;
            var ns = GetNamespace(iface);
            var fqn = ns is not null ? $"{ns}.{ifaceName}" : ifaceName;

            var methods = iface.Members.OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                foreach (var attrList in method.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        var attrName = attr.Name.ToString();
                        var verbMatch = s_refitVerbs.FirstOrDefault(v =>
                            attrName.Equals(v, StringComparison.OrdinalIgnoreCase) ||
                            attrName.Equals(v + "Attribute", StringComparison.OrdinalIgnoreCase));

                        if (verbMatch is null) continue;

                        var routeArg = attr.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                        if (routeArg is not LiteralExpressionSyntax lit) continue;
                        var route = lit.Token.ValueText;

                        var methodName = method.Identifier.Text;
                        var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        var methodId = NodeIds.ForMethod(fqn, methodName);
                        var endpointId = NodeIds.ForEndpoint(verbMatch.ToUpperInvariant(), route);

                        var methodNode = new MethodNode(methodId, methodName, csFile, line, fqn);
                        var endpointNode = new EndpointNode(endpointId,
                            $"{verbMatch.ToUpperInvariant()} {route}", csFile, line,
                            verbMatch.ToUpperInvariant(), route);

                        nodes.Add(methodNode);
                        nodes.Add(endpointNode);
                        edges.Add(new Edge(methodId, endpointId, EdgeKind.Hits, Confidence.High,
                            $"Refit [{verbMatch}(\"{route}\")] attribute on {fqn}.{methodName}"));

                        if (!refitInterfaces.TryGetValue(ifaceName, out var list))
                        {
                            list = [];
                            refitInterfaces[ifaceName] = list;
                        }
                        list.Add((methodName, verbMatch.ToUpperInvariant(), route));

                        _logger.LogDebug("Refit: {Interface}.{Method} → {Verb} {Route}",
                            fqn, methodName, verbMatch.ToUpperInvariant(), route);
                    }
                }
            }
        }
    }

    private void ScanCallers(
        string csFile,
        List<Node> nodes,
        List<Edge> edges,
        Dictionary<string, List<(string methodName, string verb, string route)>> refitInterfaces)
    {
        if (refitInterfaces.Count == 0) return;

        var source = _fs.ReadAllText(csFile);
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var ns = GetNamespace(classDecl);
            var classFqn = ns is not null ? $"{ns}.{classDecl.Identifier.Text}" : classDecl.Identifier.Text;

            // Find fields/properties whose type name is a key in refitInterfaces
            var refitFieldsByIfaceName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                var ifaceName = field.Declaration.Type.ToString();
                if (refitInterfaces.ContainsKey(ifaceName))
                {
                    if (!refitFieldsByIfaceName.TryGetValue(ifaceName, out var fieldSet))
                    {
                        fieldSet = new HashSet<string>(StringComparer.Ordinal);
                        refitFieldsByIfaceName[ifaceName] = fieldSet;
                    }
                    foreach (var variable in field.Declaration.Variables)
                        fieldSet.Add(variable.Identifier.Text);
                }
            }

            foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
            {
                var ifaceName = prop.Type.ToString();
                if (refitInterfaces.ContainsKey(ifaceName))
                {
                    if (!refitFieldsByIfaceName.TryGetValue(ifaceName, out var fieldSet))
                    {
                        fieldSet = new HashSet<string>(StringComparer.Ordinal);
                        refitFieldsByIfaceName[ifaceName] = fieldSet;
                    }
                    fieldSet.Add(prop.Identifier.Text);
                }
            }

            if (refitFieldsByIfaceName.Count == 0) continue;

            // Find invocations on those fields
            var invocations = classDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var inv in invocations)
            {
                if (inv.Expression is not MemberAccessExpressionSyntax ma) continue;

                var receiverText = ma.Expression.ToString();
                var calledMethod = ma.Name.Identifier.Text;

                // Check each Refit interface's fields to see if receiver matches
                foreach (var (ifaceName, fieldNames) in refitFieldsByIfaceName)
                {
                    var matched = fieldNames.Any(f =>
                        f.Equals(receiverText, StringComparison.OrdinalIgnoreCase) ||
                        ("_" + f).Equals(receiverText, StringComparison.OrdinalIgnoreCase) ||
                        f.Equals(receiverText.TrimStart('_'), StringComparison.OrdinalIgnoreCase));

                    if (!matched) continue;

                    var refitMethods = refitInterfaces[ifaceName];
                    var refitMethod = refitMethods.FirstOrDefault(m =>
                        m.methodName.Equals(calledMethod, StringComparison.OrdinalIgnoreCase));
                    if (refitMethod == default) continue;

                    var callerMethod = inv.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    if (callerMethod is null) continue;

                    var callerMethodName = callerMethod.Identifier.Text;
                    var callerLine = callerMethod.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var callerMethodId = NodeIds.ForMethod(classFqn, callerMethodName);
                    var refitMethodId = NodeIds.ForMethod(ifaceName, refitMethod.methodName);

                    var callerNode = new MethodNode(callerMethodId, callerMethodName, csFile, callerLine, classFqn);
                    nodes.Add(callerNode);
                    edges.Add(new Edge(callerMethodId, refitMethodId, EdgeKind.Calls, Confidence.High,
                        $"{classFqn}.{callerMethodName} calls Refit interface {ifaceName}.{refitMethod.methodName}"));
                }
            }
        }
    }

    private static string? GetNamespace(SyntaxNode node)
    {
        SyntaxNode? parent = node.Parent;
        while (parent is not null)
        {
            if (parent is FileScopedNamespaceDeclarationSyntax fsns) return fsns.Name.ToString();
            if (parent is NamespaceDeclarationSyntax ns) return ns.Name.ToString();
            parent = parent.Parent;
        }
        return null;
    }
}
