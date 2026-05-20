using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Resolvers.ViewToViewModel;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class DataTemplateResolverTests
{
    private static DataTemplateResolver BuildResolver(FakeFileSystem fs)
    {
        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        return new DataTemplateResolver(parser, NullLogger<DataTemplateResolver>.Instance);
    }

    [Fact]
    public async Task Resolve_DataTemplateWithBothViewTypeAndViewModelType_EmitsHighBindsToEdge()
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

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Resources/DataTemplates.xaml"] = xaml
        });

        var resolver = BuildResolver(fs);
        var discovery = new DiscoveryResult(["/root/Resources/DataTemplates.xaml"], [], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.High);

        var edge = result.Edges[0];
        edge.FromId.Should().Be("view:MyApp.Views.ItemView");
        edge.ToId.Should().Be("vm:MyApp.ViewModels.ItemViewModel");
        edge.Reason.Should().Contain("DataTemplate");
        edge.Reason.Should().Contain("ItemViewModel");
    }

    [Fact]
    public async Task Resolve_DataTemplateWithOnlyViewModelType_EmitsVmNodeAndNoBindsToEdge()
    {
        const string xaml = """
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                xmlns:vm="clr-namespace:MyApp.ViewModels">
              <DataTemplate DataType="{x:Type vm:ItemViewModel}">
                <TextBlock Text="{Binding Name}" />
              </DataTemplate>
            </ResourceDictionary>
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Resources/DataTemplates.xaml"] = xaml
        });

        var resolver = BuildResolver(fs);
        var discovery = new DiscoveryResult(["/root/Resources/DataTemplates.xaml"], [], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().BeEmpty("no BindsTo edge when there is no child view element");
        result.Nodes.Should().ContainSingle(n => n.Kind == NodeKind.ViewModel)
            .Which.As<ViewModelNode>().FullyQualifiedName.Should().Be("MyApp.ViewModels.ItemViewModel");
    }

    [Fact]
    public async Task Resolve_XamlWithNoDataTemplates_EmitsEmptyResult()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </UserControl>
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml
        });

        var resolver = BuildResolver(fs);
        var discovery = new DiscoveryResult(["/root/Views/LoginView.xaml"], [], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }
}
