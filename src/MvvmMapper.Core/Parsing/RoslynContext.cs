using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace MvvmMapper.Core.Parsing;

/// <summary>
/// Wraps a Roslyn <see cref="Compilation"/> and lazily caches
/// <see cref="SemanticModel"/> per syntax tree. Thread-safe.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class RoslynContext
{
    private readonly Compilation _compilation;
    private readonly ILogger<RoslynContext> _logger;
    private readonly Dictionary<SyntaxTree, SemanticModel> _modelCache = new();
    private readonly object _lock = new();

    public RoslynContext(Compilation compilation, ILogger<RoslynContext> logger)
    {
        _compilation = compilation;
        _logger = logger;
    }

    /// <summary>The underlying Roslyn compilation.</summary>
    public Compilation Compilation => _compilation;

    /// <summary>
    /// Returns a cached <see cref="SemanticModel"/> for the given syntax tree,
    /// creating it on first access. Safe to call from multiple threads.
    /// </summary>
    public SemanticModel GetSemanticModel(SyntaxTree syntaxTree)
    {
        lock (_lock)
        {
            if (!_modelCache.TryGetValue(syntaxTree, out var model))
            {
                model = _compilation.GetSemanticModel(syntaxTree);
                _modelCache[syntaxTree] = model;
            }
            return model;
        }
    }

    /// <summary>Returns all named types in the compilation (across all namespaces, including nested types).</summary>
    public IEnumerable<INamedTypeSymbol> GetAllNamedTypes() =>
        GetAllNamedTypesInNamespace(_compilation.GlobalNamespace);

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypesInNamespace(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
                yield return nested;
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        foreach (var type in GetAllNamedTypesInNamespace(childNs))
            yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deep in GetNestedTypes(nested))
                yield return deep;
        }
    }
}
