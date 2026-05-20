using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Resolvers.ViewToViewModel;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class LocatorResolverTests
{
    [Fact]
    public async Task Resolve_LocatorBindingWithResolvedProperty_EmitsHighConfidenceEdge()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         DataContext="{Binding LoginVM, Source={StaticResource Locator}}">
            </UserControl>
            """;

        const string locatorCs = """
            namespace MyApp;
            public class ViewModelLocator
            {
                public LoginViewModel LoginVM => new LoginViewModel();
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/ViewModelLocator.cs"] = locatorCs
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var config = new MvvmMapConfig(); // LocatorClasses = ["ViewModelLocator"] by default
        var resolver = new LocatorResolver(config, parser, fs, NullLogger<LocatorResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/ViewModelLocator.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.High);

        var edge = result.Edges[0];
        edge.FromId.Should().Be("view:MyApp.Views.LoginView");
        edge.ToId.Should().Be("vm:LoginViewModel");
        edge.Reason.Should().Contain("Locator binding");
    }

    [Fact]
    public async Task Resolve_LocatorBindingWithUnresolvedProperty_EmitsLowConfidenceEdge()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         DataContext="{Binding LoginVM, Source={StaticResource Locator}}">
            </UserControl>
            """;

        // No locator file provided
        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var config = new MvvmMapConfig();
        var resolver = new LocatorResolver(config, parser, fs, NullLogger<LocatorResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            [],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.Low);

        result.Edges[0].Reason.Should().Contain("could not be resolved");
    }

    [Fact]
    public async Task Resolve_NoLocatorBinding_EmitsNoEdge()
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

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var config = new MvvmMapConfig();
        var resolver = new LocatorResolver(config, parser, fs, NullLogger<LocatorResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            [],
            "/root");

        var result = await resolver.ResolveAsync(discovery);
        result.Edges.Should().BeEmpty();
    }
}
