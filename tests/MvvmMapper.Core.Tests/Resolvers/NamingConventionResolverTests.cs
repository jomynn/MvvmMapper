using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Resolvers.ViewToViewModel;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class NamingConventionResolverTests
{
    [Fact]
    public async Task Resolve_MatchingViewAndViewModel_EmitsLowConfidenceEdge()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </UserControl>
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/ViewModels/LoginViewModel.cs"] = "class LoginViewModel {}"
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var resolver = new NamingConventionResolver(
            new MvvmMapConfig(), parser, fs,
            NullLogger<NamingConventionResolver>.Instance);

        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/ViewModels/LoginViewModel.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.Low);
        result.Edges[0].Reason.Should().Contain("Naming convention");
    }

    [Fact]
    public async Task Resolve_CaliburnMicroDetected_EmitsMediumEdgeInAdditionToLow()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </UserControl>
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/ViewModels/LoginViewModel.cs"] = "class LoginViewModel {}",
            ["/root/Bootstrapper.cs"] = "using Caliburn.Micro; public class Bootstrapper {}"
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var resolver = new NamingConventionResolver(
            new MvvmMapConfig(), parser, fs,
            NullLogger<NamingConventionResolver>.Instance);

        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/ViewModels/LoginViewModel.cs", "/root/Bootstrapper.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().Contain(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.Low,
            "low-confidence naming-convention edge must still be present");

        result.Edges.Should().Contain(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.Medium &&
            e.Reason.Contains("Caliburn.Micro"),
            "medium-confidence edge must be emitted when Caliburn.Micro is detected");
    }

    [Fact]
    public async Task Resolve_NoMatchingViewModel_EmitsNoEdge()
    {
        const string xaml = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </UserControl>
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/Services/AuthService.cs"] = "class AuthService {}"
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var resolver = new NamingConventionResolver(
            new MvvmMapConfig(), parser, fs,
            NullLogger<NamingConventionResolver>.Instance);

        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/Services/AuthService.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);
        result.Edges.Should().BeEmpty();
    }
}
