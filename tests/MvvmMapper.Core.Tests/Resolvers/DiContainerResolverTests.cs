using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Resolvers.ViewToViewModel;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class DiContainerResolverTests
{
    [Fact]
    public async Task Resolve_NoBootstrapFiles_ReturnsEmpty()
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
                public LoginView(LoginViewModel vm) { }
            }
            """;

        // No App.xaml.cs / Startup.cs / Program.cs / Bootstrapper.cs
        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/Views/LoginView.xaml.cs"] = codeBehind,
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var config = new MvvmMapConfig();
        var resolver = new DiContainerResolver(config, parser, fs, NullLogger<DiContainerResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/Views/LoginView.xaml.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_AppXamlCsRegistersViewModel_EmitsMediumConfidenceEdge()
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

        // App.xaml.cs registers both types
        const string appXamlCs = """
            using Microsoft.Extensions.DependencyInjection;
            namespace MyApp;
            public partial class App
            {
                protected override void OnStartup(StartupEventArgs e)
                {
                    var services = new ServiceCollection();
                    services.AddSingleton<LoginView>();
                    services.AddSingleton<LoginViewModel>();
                }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/Views/LoginView.xaml.cs"] = codeBehind,
            ["/root/App.xaml.cs"] = appXamlCs,
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var config = new MvvmMapConfig();
        var resolver = new DiContainerResolver(config, parser, fs, NullLogger<DiContainerResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/Views/LoginView.xaml.cs", "/root/App.xaml.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e =>
            e.Kind == EdgeKind.BindsTo &&
            e.Confidence == Confidence.Medium);

        var edge = result.Edges[0];
        edge.ToId.Should().Be("vm:LoginViewModel");
        edge.Reason.Should().Contain("DI");
    }

    [Fact]
    public async Task Resolve_BootstrapRegistersOnlyNonVmType_EmitsNoEdge()
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
                public LoginView(IAuthService svc) { }
            }
            """;

        // App.xaml.cs registers a service (not a ViewModel suffix)
        const string appXamlCs = """
            using Microsoft.Extensions.DependencyInjection;
            namespace MyApp;
            public partial class App
            {
                protected override void OnStartup(StartupEventArgs e)
                {
                    var services = new ServiceCollection();
                    services.AddSingleton<IAuthService>();
                }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/Views/LoginView.xaml"] = xaml,
            ["/root/Views/LoginView.xaml.cs"] = codeBehind,
            ["/root/App.xaml.cs"] = appXamlCs,
        });

        var parser = new XamlParser(fs, NullLogger<XamlParser>.Instance);
        var config = new MvvmMapConfig();
        var resolver = new DiContainerResolver(config, parser, fs, NullLogger<DiContainerResolver>.Instance);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/Views/LoginView.xaml.cs", "/root/App.xaml.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        // IAuthService doesn't match ViewModel suffix — no edge emitted
        result.Edges.Should().BeEmpty();
    }
}
