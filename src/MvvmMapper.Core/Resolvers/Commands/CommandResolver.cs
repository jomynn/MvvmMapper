using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;

namespace MvvmMapper.Core.Resolvers.Commands;

public sealed class CommandResolver : IResolver
{
    private static readonly string[] s_commandCtorTypes =
        ["RelayCommand", "DelegateCommand", "AsyncRelayCommand", "AsyncDelegateCommand",
         "RelayCommand`1", "DelegateCommand`1", "AsyncRelayCommand`1"];

    private readonly MvvmMapConfig _config;
    private readonly XamlParser _xamlParser;
    private readonly IFileSystem _fs;
    private readonly ILogger<CommandResolver> _logger;

    public CommandResolver(
        MvvmMapConfig config,
        XamlParser xamlParser,
        IFileSystem fs,
        ILogger<CommandResolver> logger)
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

        // commandName → (vmId, methodNode) — keyed by short command property name e.g. "LoginCommand"
        var commandMap = new Dictionary<string, (string vmId, MethodNode methodNode)>(StringComparer.Ordinal);

        var vmFiles = discovery.CsFiles
            .Where(f => IsViewModelFile(_fs.GetFileNameWithoutExtension(f)))
            .ToList();

        foreach (var csFile in vmFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ProcessViewModelFile(csFile, nodes, edges, commandMap);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CommandResolver failed processing {File}", csFile);
            }
        }

        // Step 2: match XAML Command bindings to resolved methods
        foreach (var xamlFile in discovery.XamlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var doc = _xamlParser.TryParse(xamlFile);
            if (doc is null || doc.CommandBindings.Count == 0) continue;

            var viewId = NodeIds.ForView(doc);
            var viewNode = new ViewNode(viewId, doc.XClass ?? xamlFile, xamlFile, null, doc.XClass ?? string.Empty);
            nodes.Add(viewNode);

            foreach (var binding in doc.CommandBindings)
            {
                if (commandMap.TryGetValue(binding.CommandName, out var entry))
                {
                    edges.Add(new Edge(
                        viewId, entry.methodNode.Id, EdgeKind.Invokes, Confidence.High,
                        $"XAML Command=\"{{Binding {binding.CommandName}}}\" on <{binding.ElementType}> resolved to {entry.methodNode.OwningType}.{entry.methodNode.DisplayName}"));
                }
                else
                {
                    _logger.LogDebug(
                        "CommandResolver: no method found for command {Command} in {File}",
                        binding.CommandName, xamlFile);
                }
            }
        }

        return Task.FromResult(new ResolverResult(nodes, edges));
    }

    private void ProcessViewModelFile(
        string csFile,
        List<Node> nodes,
        List<Edge> edges,
        Dictionary<string, (string vmId, MethodNode methodNode)> commandMap)
    {
        var source = _fs.ReadAllText(csFile);
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var root = syntaxTree.GetCompilationUnitRoot();

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var className = classDecl.Identifier.Text;
            var ns = GetNamespace(classDecl);
            var fqn = ns is not null ? $"{ns}.{className}" : className;
            var vmId = NodeIds.ForViewModel(fqn);

            // Pattern B: [RelayCommand] attribute on a method
            var relayCommandMethods = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(a => a.Name.ToString().Contains("RelayCommand")))
                .ToList();

            foreach (var method in relayCommandMethods)
            {
                var methodName = method.Identifier.Text;
                var baseName = methodName.EndsWith("Async", StringComparison.OrdinalIgnoreCase)
                    ? methodName[..^5]
                    : methodName;
                var commandName = baseName + "Command";
                var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                var methodNode = new MethodNode(
                    NodeIds.ForMethod(fqn, methodName),
                    methodName, csFile, line, fqn);

                nodes.Add(methodNode);
                edges.Add(new Edge(vmId, methodNode.Id, EdgeKind.Contains, Confidence.High,
                    $"[RelayCommand] generator: {methodName} → {commandName}"));

                commandMap[commandName] = (vmId, methodNode);
                _logger.LogDebug("CommandResolver: [RelayCommand] {VM}.{Method} → {Cmd}", fqn, methodName, commandName);
            }

            // Patterns A & C: ICommand properties
            var commandProps = classDecl.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => IsCommandType(p.Type.ToString()))
                .ToList();

            foreach (var prop in commandProps)
            {
                var commandName = prop.Identifier.Text;
                var line = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                var executeName = TryFindExecuteInInitializer(prop)
                    ?? TryFindExecuteInConstructor(classDecl, commandName);

                MethodNode methodNode;
                if (executeName is not null)
                {
                    var executeDecl = classDecl.Members
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(m => m.Identifier.Text.Equals(executeName, StringComparison.Ordinal));

                    var executeLine = executeDecl is not null
                        ? (int?)(executeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
                        : null;

                    methodNode = new MethodNode(
                        NodeIds.ForMethod(fqn, executeName),
                        executeName, csFile, executeLine ?? line, fqn);

                    nodes.Add(methodNode);
                    edges.Add(new Edge(vmId, methodNode.Id, EdgeKind.Contains, Confidence.High,
                        $"ICommand property {commandName} delegates to {executeName}"));
                }
                else
                {
                    methodNode = new MethodNode(
                        NodeIds.ForMethod(fqn, commandName),
                        commandName, csFile, line, fqn);

                    nodes.Add(methodNode);
                    edges.Add(new Edge(vmId, methodNode.Id, EdgeKind.Contains, Confidence.Medium,
                        $"ICommand property {commandName} without resolved Execute delegate"));
                }

                commandMap[commandName] = (vmId, methodNode);
            }
        }
    }

    private static string? TryFindExecuteInInitializer(PropertyDeclarationSyntax prop)
    {
        var initializer = prop.Initializer?.Value;
        return initializer is ObjectCreationExpressionSyntax objCreation
            ? ExtractFirstArgumentName(objCreation)
            : null;
    }

    private static string? TryFindExecuteInConstructor(ClassDeclarationSyntax classDecl, string commandName)
    {
        foreach (var ctor in classDecl.Members.OfType<ConstructorDeclarationSyntax>())
        {
            if (ctor.Body is null) continue;

            foreach (var stmt in ctor.Body.Statements.OfType<ExpressionStatementSyntax>())
            {
                if (stmt.Expression is not AssignmentExpressionSyntax assignment) continue;

                var left = assignment.Left.ToString().Trim();
                if (!left.Equals(commandName, StringComparison.Ordinal) &&
                    !left.Equals($"this.{commandName}", StringComparison.Ordinal))
                    continue;

                if (assignment.Right is ObjectCreationExpressionSyntax objCreation)
                    return ExtractFirstArgumentName(objCreation);
            }
        }
        return null;
    }

    private static string? ExtractFirstArgumentName(ObjectCreationExpressionSyntax objCreation)
    {
        var firstArg = objCreation.ArgumentList?.Arguments.FirstOrDefault();
        return firstArg?.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            _ => null
        };
    }

    private static string? GetNamespace(ClassDeclarationSyntax classDecl)
    {
        SyntaxNode? parent = classDecl.Parent;
        while (parent is not null)
        {
            if (parent is FileScopedNamespaceDeclarationSyntax fsns)
                return fsns.Name.ToString();
            if (parent is NamespaceDeclarationSyntax ns)
                return ns.Name.ToString();
            parent = parent.Parent;
        }
        return null;
    }

    private static bool IsCommandType(string typeName)
    {
        var t = typeName.TrimEnd('?');
        return t is "ICommand" or "IAsyncRelayCommand" or "IRelayCommand"
            || t.EndsWith("Command", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsViewModelFile(string fileName) =>
        _config.Patterns.ViewModelSuffix.Any(s =>
            fileName.EndsWith(s, StringComparison.OrdinalIgnoreCase));
}
