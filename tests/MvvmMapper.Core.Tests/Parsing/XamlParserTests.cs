using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Parsing;
using Xunit;

namespace MvvmMapper.Core.Tests.Parsing;

public sealed class XamlParserTests
{
    private XamlParser BuildParser() =>
        new(new FakeFileSystem([]), NullLogger<XamlParser>.Instance);

    [Fact]
    public void Parse_ExplicitDataContextElement_ExtractsTypeAndNamespace()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:vm="clr-namespace:MyApp.ViewModels">
              <UserControl.DataContext>
                <vm:LoginViewModel />
              </UserControl.DataContext>
            </UserControl>
            """;

        var doc = BuildParser().Parse("LoginView.xaml", xaml);

        doc.DataContexts.Should().ContainSingle();
        var dc = doc.DataContexts[0];
        dc.TypeName.Should().Be("LoginViewModel");
        dc.ClrNamespace.Should().Be("MyApp.ViewModels");
        dc.Kind.Should().Be(DataContextKind.ExplicitElement);
    }

    [Fact]
    public void Parse_LocatorBinding_ExtractsLocatorPattern()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         DataContext="{Binding LoginVM, Source={StaticResource Locator}}">
            </UserControl>
            """;

        var doc = BuildParser().Parse("LoginView.xaml", xaml);

        doc.DataContexts.Should().ContainSingle();
        doc.DataContexts[0].Kind.Should().Be(DataContextKind.LocatorBinding);
        doc.DataContexts[0].TypeName.Should().Be("LoginVM");
    }

    [Fact]
    public void Parse_CommandBinding_ExtractsCommandName()
    {
        const string xaml = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Button Command="{Binding LoginCommand}" Content="Login" />
            </UserControl>
            """;

        var doc = BuildParser().Parse("LoginView.xaml", xaml);

        doc.CommandBindings.Should().ContainSingle();
        doc.CommandBindings[0].CommandName.Should().Be("LoginCommand");
        doc.CommandBindings[0].ElementType.Should().Be("Button");
    }

    [Fact]
    public void Parse_DataTemplate_ExtractsViewAndViewModel()
    {
        const string xaml = """
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                xmlns:vm="clr-namespace:MyApp.ViewModels"
                                xmlns:views="clr-namespace:MyApp.Views">
              <DataTemplate DataType="{x:Type vm:ItemViewModel}">
                <views:ItemView />
              </DataTemplate>
            </ResourceDictionary>
            """;

        var doc = BuildParser().Parse("Templates.xaml", xaml);

        doc.DataTemplates.Should().ContainSingle();
        doc.DataTemplates[0].ViewModelTypeName.Should().Be("ItemViewModel");
        doc.DataTemplates[0].ViewModelClrNamespace.Should().Be("MyApp.ViewModels");
        doc.DataTemplates[0].ViewTypeName.Should().Be("ItemView");
    }

    [Fact]
    public void Parse_XClass_IsExtracted()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </UserControl>
            """;

        var doc = BuildParser().Parse("LoginView.xaml", xaml);
        doc.XClass.Should().Be("MyApp.Views.LoginView");
    }

    [Fact]
    public void Parse_MultipleCommandBindings_AllExtracted()
    {
        const string xaml = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <StackPanel>
                <Button Command="{Binding LoginCommand}" />
                <Button Command="{Binding RegisterCommand}" />
              </StackPanel>
            </UserControl>
            """;

        var doc = BuildParser().Parse("View.xaml", xaml);

        doc.CommandBindings.Should().HaveCount(2);
        doc.CommandBindings.Select(c => c.CommandName).Should()
            .Contain("LoginCommand").And.Contain("RegisterCommand");
    }

    [Fact]
    public void Parse_XmlnsMap_ContainsClrNamespace()
    {
        const string xaml = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:vm="clr-namespace:MyApp.ViewModels;assembly=MyApp">
            </UserControl>
            """;

        var doc = BuildParser().Parse("View.xaml", xaml);

        doc.XmlnsMap.Should().ContainKey("vm");
        doc.XmlnsMap["vm"].Should().Be("MyApp.ViewModels");
    }

    [Fact]
    public void Parse_ChildCustomControl_IsRecorded()
    {
        const string xaml = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:shared="clr-namespace:MyApp.Shared.Controls">
              <shared:UserAvatar />
            </UserControl>
            """;

        var doc = BuildParser().Parse("MainView.xaml", xaml);

        doc.ChildControlTypeNames.Should().Contain("UserAvatar");
    }

    [Fact]
    public void Parse_BindingPathDataContext_ExtractsPath()
    {
        const string xaml = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         DataContext="{Binding ChildViewModel}">
            </UserControl>
            """;

        var doc = BuildParser().Parse("View.xaml", xaml);

        doc.DataContexts.Should().ContainSingle();
        doc.DataContexts[0].Kind.Should().Be(DataContextKind.BindingPath);
        doc.DataContexts[0].TypeName.Should().Be("ChildViewModel");
    }

    [Fact]
    public void Parse_RootElementType_IsExtracted()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </Window>
            """;

        var doc = BuildParser().Parse("MainWindow.xaml", xaml);
        doc.RootElementType.Should().Be("Window");
    }
}
