namespace MvvmMapper.Core.Parsing;

/// <summary>
/// Parsed representation of a XAML file.
/// <para><c>XClass</c>: The x:Class value, e.g. "MyApp.Views.LoginView".</para>
/// <para><c>RootElementType</c>: The root element type (UserControl, Window, Page, etc.).</para>
/// <para><c>XmlnsMap</c>: All xmlns prefix to CLR namespace mappings declared on the root element.</para>
/// <para><c>ChildControlTypeNames</c>: Child UserControl/custom-element type references found as child elements.</para>
/// </summary>
public sealed record XamlDocument(
    string FilePath,
    string? XClass,
    string RootElementType,
    IReadOnlyDictionary<string, string> XmlnsMap,
    IReadOnlyList<XamlDataContextInfo> DataContexts,
    IReadOnlyList<XamlCommandBinding> CommandBindings,
    IReadOnlyList<XamlDataTemplateInfo> DataTemplates,
    IReadOnlyList<string> ChildControlTypeNames);
