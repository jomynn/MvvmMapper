namespace MvvmMapper.Core.Parsing;

/// <summary>A Command="{Binding XxxCommand}" binding found in XAML.</summary>
public sealed record XamlCommandBinding(
    string CommandName,
    string ElementType,
    int LineNumber);
