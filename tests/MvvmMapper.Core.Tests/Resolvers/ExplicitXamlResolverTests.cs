using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Resolvers.ViewToViewModel;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class ExplicitXamlResolverTests
{
    private static FakeFileSystem BuildFs(string xamlContent) =>
        new(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xamlContent
        });

    [Fact]
    public async Task Resolve_ExplicitXamlDataContext_EmitsHighConfidenceEdge()
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

        var fs = BuildFs(xaml);
        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var resolver = new ExplicitXamlResolver(parser, NullLogger<ExplicitXamlResolver>.Instance);
        var discovery = new DiscoveryResult(["/root/Views/LoginView.xaml"], [], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.High);

        var edge = result.Edges[0];
        edge.FromId.Should().Be("view:MyApp.Views.LoginView");
        edge.ToId.Should().Be("vm:MyApp.ViewModels.LoginViewModel");
        edge.Reason.Should().Contain("Explicit");
    }

    [Fact]
    public async Task Resolve_NoDataContext_EmitsNoEdge()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </UserControl>
            """;

        var fs = BuildFs(xaml);
        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var resolver = new ExplicitXamlResolver(parser, NullLogger<ExplicitXamlResolver>.Instance);
        var discovery = new DiscoveryResult(["/root/Views/LoginView.xaml"], [], "/root");

        var result = await resolver.ResolveAsync(discovery);
        result.Edges.Should().BeEmpty();
    }
}
