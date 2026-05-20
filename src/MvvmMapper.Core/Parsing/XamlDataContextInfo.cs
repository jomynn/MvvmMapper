namespace MvvmMapper.Core.Parsing;

/// <summary>
/// A DataContext declaration found in a XAML file.
/// <para><c>TypeName</c>: The resolved ViewModel class name (e.g. "LoginViewModel").</para>
/// <para><c>ClrNamespace</c>: The CLR namespace resolved from the xmlns alias (e.g. "MyApp.ViewModels").</para>
/// <para><c>Kind</c>: How the DataContext was declared.</para>
/// </summary>
public sealed record XamlDataContextInfo(
    string TypeName,
    string? ClrNamespace,
    DataContextKind Kind,
    int LineNumber);

public enum DataContextKind
{
    /// <summary>&lt;UserControl.DataContext&gt;&lt;vm:Foo /&gt;&lt;/UserControl.DataContext&gt;</summary>
    ExplicitElement,
    /// <summary>DataContext="{Binding Foo, Source={StaticResource Locator}}"</summary>
    LocatorBinding,
    /// <summary>DataContext="{Binding SomeChildVM}" — composition edge</summary>
    BindingPath,
    /// <summary>d:DataContext — design-time only, ignored during analysis</summary>
    DesignTime,
}
