using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Resolvers.ViewToViewModel;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class CodeBehindResolverTests
{
    [Fact]
    public async Task Resolve_NewViewModelInConstructor_EmitsHighConfidenceEdge()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </UserControl>
            """;

        const string codeBehind = """
            namespace MyApp.Views;
            public partial class LoginView
            {
                public LoginView()
                {
                    InitializeComponent();
                    this.DataContext = new LoginViewModel();
                }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/Views/LoginView.xaml.cs"] = codeBehind
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var resolver = new CodeBehindResolver(fs, parser, NullLogger<CodeBehindResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/Views/LoginView.xaml.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.High);

        var edge = result.Edges[0];
        edge.FromId.Should().Be("view:MyApp.Views.LoginView");
        edge.ToId.Should().Be("vm:LoginViewModel");
        edge.Reason.Should().Contain("DataContext assigned in code-behind");
    }

    [Fact]
    public async Task Resolve_ConstructorInjectedViewModel_EmitsHighConfidenceEdge()
    {
        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </UserControl>
            """;

        const string codeBehind = """
            namespace MyApp.Views;
            public partial class LoginView
            {
                public LoginView(LoginViewModel vm)
                {
                    InitializeComponent();
                    DataContext = vm;
                }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/Views/LoginView.xaml.cs"] = codeBehind
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var resolver = new CodeBehindResolver(fs, parser, NullLogger<CodeBehindResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/Views/LoginView.xaml.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().HaveCountGreaterOrEqualTo(1);
        result.Edges.Should().Contain(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.High &&
            e.ToId == "vm:LoginViewModel");
    }

    [Fact]
    public async Task Resolve_NoCsFile_EmitsNoEdge()
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
        var resolver = new CodeBehindResolver(fs, parser, NullLogger<CodeBehindResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            [],
            "/root");

        var result = await resolver.ResolveAsync(discovery);
        result.Edges.Should().BeEmpty();
    }
}
