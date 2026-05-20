namespace MvvmMapper.Core.Parsing;

/// <summary>A DataTemplate with a DataType attribute.</summary>
public sealed record XamlDataTemplateInfo(
    string ViewTypeName,
    string? ViewClrNamespace,
    string ViewModelTypeName,
    string? ViewModelClrNamespace,
    int LineNumber);
