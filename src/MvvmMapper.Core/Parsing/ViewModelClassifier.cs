using Microsoft.CodeAnalysis;
using MvvmMapper.Core.Configuration;

namespace MvvmMapper.Core.Parsing;

/// <summary>
/// Identifies which named types in a compilation are ViewModels, based on
/// configured name suffixes and base types.
/// </summary>
public sealed class ViewModelClassifier
{
    private readonly PatternConfig _config;

    public ViewModelClassifier(PatternConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Returns true when the given type should be treated as a ViewModel:
    /// concrete (non-abstract) class whose name ends with a configured suffix
    /// OR that inherits (directly or transitively) a configured base type.
    /// </summary>
    public bool IsViewModel(INamedTypeSymbol type)
    {
        if (type.IsAbstract || type.TypeKind != TypeKind.Class) return false;

        if (HasViewModelSuffix(type.Name)) return true;
        if (InheritsViewModelBase(type)) return true;

        return false;
    }

    /// <summary>Returns true when the type name ends with a configured view suffix.</summary>
    public bool IsView(string typeName) =>
        _config.ViewSuffix.Any(suffix =>
            typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private bool HasViewModelSuffix(string name) =>
        _config.ViewModelSuffix.Any(suffix =>
            name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private bool InheritsViewModelBase(INamedTypeSymbol type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (_config.ViewModelBaseTypes.Any(bt =>
                    baseType.Name.Equals(bt, StringComparison.OrdinalIgnoreCase)))
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }
}
