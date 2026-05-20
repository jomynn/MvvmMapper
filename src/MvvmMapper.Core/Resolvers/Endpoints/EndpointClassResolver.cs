using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Resolvers.Endpoints;

/// <summary>
/// Handles endpoint classes that define routes in a static Dictionary property:
///
///   public class ForeignDraftEndpoint
///   {
///       public static Dictionary&lt;RequestCode, string&gt; Urls { get; } = new() {
///           [RequestCode.IssueForeignDraft] = "/api/foreign-draft/issue",
///       };
///   }
///
/// HTTP verb is inferred from the enum key name prefix.
/// Also connects HttpClient callers that pass ClassName.Urls[Key] as the URL argument.
/// </summary>
public sealed class EndpointClassResolver : IResolver
{
    private static readonly string[] s_urlPropertyNames =
        ["Urls", "Routes", "Endpoints", "Paths", "ApiPaths", "ApiRoutes", "ApiUrls"];

    private readonly IFileSystem _fs;
    private readonly ILogger<EndpointClassResolver> _logger;

    public EndpointClassResolver(IFileSystem fs, ILogger<EndpointClassResolver> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public Task<ResolverResult> ResolveAsync(DiscoveryResult discovery, CancellationToken cancellationToken = default)
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        // Pass 1: collect endpoint class definitions → (className → list of (key, verb, route))
        // Key format used later: "ClassName.PropertyName[EnumKey]"
        var endpointMap = new Dictionary<string, List<EndpointEntry>>(StringComparer.Ordinal);

        foreach (var csFile in discovery.CsFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { ScanEndpointClass(csFile, nodes, edges, endpointMap); }
            catch (Exception ex) { _logger.LogError(ex, "EndpointClassResolver scan failed on {File}", csFile); }
        }

        // Pass 2: connect HttpClient callers that use ClassName.Urls[Key] as URL argument
        if (endpointMap.Count > 0)
        {
            foreach (var csFile in discovery.CsFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { ScanCallers(csFile, nodes, edges, endpointMap); }
                catch (Exception ex) { _logger.LogError(ex, "EndpointClassResolver caller scan failed on {File}", csFile); }
            }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }

    private void ScanEndpointClass(
        string csFile,
        List<Node> nodes,
        List<Edge> edges,
        Dictionary<string, List<EndpointEntry>> endpointMap)
    {
        var source = _fs.ReadAllText(csFile);
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var className = classDecl.Identifier.Text;
            if (!className.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase) &&
                !className.EndsWith("Endpoints", StringComparison.OrdinalIgnoreCase))
                continue;

            var ns = GetNamespace(classDecl);
            var fqn = ns is not null ? $"{ns}.{className}" : className;
            var classLine = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (!IsStaticDictionaryWithStringValue(prop)) continue;
                if (!s_urlPropertyNames.Contains(prop.Identifier.Text, StringComparer.OrdinalIgnoreCase)) continue;

                var propName = prop.Identifier.Text;
                var entries = ExtractDictionaryEntries(prop, csFile, classLine, fqn, className, propName);

                if (entries.Count == 0) continue;

                foreach (var entry in entries)
                {
                    nodes.Add(entry.EndpointNode);
                    _logger.LogDebug("EndpointClass: {Class}.{Prop}[{Key}] → {Verb} {Route}",
                        className, propName, entry.DictionaryKey, entry.Verb, entry.Route);
                }

                var mapKey = $"{className}.{propName}";
                if (!endpointMap.TryGetValue(mapKey, out var list))
                {
                    list = [];
                    endpointMap[mapKey] = list;
                }
                list.AddRange(entries);
            }
        }
    }

    private static List<EndpointEntry> ExtractDictionaryEntries(
        PropertyDeclarationSyntax prop,
        string csFile,
        int classLine,
        string fqn,
        string className,
        string propName)
    {
        var results = new List<EndpointEntry>();

        // Find the initializer expression — handles both arrow getter and property initializer
        InitializerExpressionSyntax? initializer = null;

        if (prop.Initializer?.Value is ObjectCreationExpressionSyntax objCreate)
            initializer = objCreate.Initializer;
        else if (prop.Initializer?.Value is ImplicitObjectCreationExpressionSyntax implicitCreate)
            initializer = implicitCreate.Initializer;

        if (initializer is null) return results;

        foreach (var exprSyntax in initializer.Expressions)
        {
            // Pattern: [EnumKey] = "route string"
            if (exprSyntax is not AssignmentExpressionSyntax assignment) continue;

            // LHS must be an element-access bracketed expression: [RequestCode.Something]
            if (assignment.Left is not ImplicitElementAccessSyntax bracketAccess) continue;
            var keyArg = bracketAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (keyArg is null) continue;

            // RHS must be a string literal
            if (assignment.Right is not LiteralExpressionSyntax lit ||
                !lit.IsKind(SyntaxKind.StringLiteralExpression))
                continue;

            var route = lit.Token.ValueText;
            var keyText = keyArg.ToString(); // e.g. "RequestCode.IssueForeignDraft"
            var enumMember = keyText.Contains('.') ? keyText[(keyText.LastIndexOf('.') + 1)..] : keyText;
            var verb = InferVerbFromName(enumMember);
            var line = exprSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            var endpointId = NodeIds.ForEndpoint(verb, route);
            var endpointNode = new EndpointNode(endpointId, $"{verb} {route}", csFile, line, verb, route);

            results.Add(new EndpointEntry(
                DictionaryKey: keyText,
                Verb: verb,
                Route: route,
                EndpointNode: endpointNode,
                PropName: propName,
                ClassName: className));
        }

        return results;
    }

    private void ScanCallers(
        string csFile,
        List<Node> nodes,
        List<Edge> edges,
        Dictionary<string, List<EndpointEntry>> endpointMap)
    {
        var source = _fs.ReadAllText(csFile);
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in invocations)
        {
            if (inv.Expression is not MemberAccessExpressionSyntax ma) continue;

            var methodName = ma.Name.Identifier.Text;
            if (!IsHttpClientMethod(methodName)) continue;

            var urlArg = inv.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (urlArg is null) continue;

            // Pattern: ClassName.PropertyName[EnumKey]
            // Syntax: ElementAccessExpression { Expression = MemberAccess(Class, Prop), Arguments = [key] }
            if (urlArg is not ElementAccessExpressionSyntax elemAccess) continue;

            var innerAccess = elemAccess.Expression as MemberAccessExpressionSyntax;
            if (innerAccess is null) continue;

            var accessedClass = innerAccess.Expression.ToString();
            var accessedProp = innerAccess.Name.Identifier.Text;
            var mapKey = $"{accessedClass}.{accessedProp}";

            if (!endpointMap.TryGetValue(mapKey, out var entries)) continue;

            var keyExpr = elemAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression?.ToString();
            var matchedEntry = keyExpr is not null
                ? entries.FirstOrDefault(e => e.DictionaryKey.Equals(keyExpr, StringComparison.Ordinal))
                : null;

            var callerMethod = inv.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var callerClass = inv.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (callerMethod is null || callerClass is null) continue;

            var callerNs = GetNamespace(callerClass);
            var callerFqn = callerNs is not null ? $"{callerNs}.{callerClass.Identifier.Text}" : callerClass.Identifier.Text;
            var callerMethodName = callerMethod.Identifier.Text;
            var callerLine = callerMethod.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var invLine = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            var callerMethodId = NodeIds.ForMethod(callerFqn, callerMethodName);
            var callerNode = new MethodNode(callerMethodId, callerMethodName, csFile, callerLine, callerFqn);
            nodes.Add(callerNode);

            if (matchedEntry is not null)
            {
                // Exact key match — connect directly to the known endpoint
                edges.Add(new Edge(callerMethodId, matchedEntry.EndpointNode.Id, EdgeKind.Hits, Confidence.Medium,
                    $"{callerFqn}.{callerMethodName} calls HttpClient with {accessedClass}.{accessedProp}[{keyExpr}] at line {invLine}"));
                _logger.LogDebug("EndpointClass caller: {Caller} → {Verb} {Route} [Medium]",
                    callerMethodId, matchedEntry.Verb, matchedEntry.Route);
            }
            else
            {
                // Key unknown at analysis time — emit Low-confidence edges to all entries in the map
                foreach (var entry in entries)
                {
                    edges.Add(new Edge(callerMethodId, entry.EndpointNode.Id, EdgeKind.Hits, Confidence.Low,
                        $"{callerFqn}.{callerMethodName} calls HttpClient with {accessedClass}.{accessedProp}[?] at line {invLine}; key not statically resolvable"));
                    _logger.LogDebug("EndpointClass caller (unknown key): {Caller} → {Verb} {Route} [Low]",
                        callerMethodId, entry.Verb, entry.Route);
                }
            }
        }
    }

    private static bool IsStaticDictionaryWithStringValue(PropertyDeclarationSyntax prop)
    {
        var isStatic = prop.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
        if (!isStatic) return false;

        // Accept Dictionary<X, string>, IDictionary<X, string>, IReadOnlyDictionary<X, string>
        var typeStr = prop.Type.ToString();
        return typeStr.Contains("Dictionary") && typeStr.EndsWith("string>", StringComparison.Ordinal);
    }

    private static bool IsHttpClientMethod(string name) => name is
        "GetAsync" or "PostAsync" or "PutAsync" or "DeleteAsync" or
        "PatchAsync" or "HeadAsync" or "SendAsync";

    private static string InferVerbFromName(string enumMember)
    {
        if (string.IsNullOrEmpty(enumMember)) return "POST";

        // Normalise: PascalCase "IssueForeignDraft" → compare prefix segments
        var lower = enumMember.ToLowerInvariant();

        if (StartsWithAny(lower, ["get", "fetch", "list", "load", "query", "search", "find", "read", "retrieve"]))
            return "GET";
        if (StartsWithAny(lower, ["delete", "remove", "cancel", "revoke", "deactivate", "disable"]))
            return "DELETE";
        if (StartsWithAny(lower, ["patch"]))
            return "PATCH";
        if (StartsWithAny(lower, ["put", "replace", "set"]))
            return "PUT";

        // Default: create/issue/submit/add/send/update/edit and everything else → POST
        return "POST";
    }

    private static bool StartsWithAny(string value, string[] prefixes)
    {
        foreach (var prefix in prefixes)
            if (value.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
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

    private sealed record EndpointEntry(
        string DictionaryKey,
        string Verb,
        string Route,
        EndpointNode EndpointNode,
        string PropName,
        string ClassName);
}
