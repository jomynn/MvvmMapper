using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Configuration;
using MvvmMapper.Core.Discovery;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Parsing;
using MvvmMapper.Core.Resolvers.Commands;
using Xunit;

namespace MvvmMapper.Core.Tests.Resolvers;

public sealed class CommandResolverTests
{
    private static CommandResolver BuildResolver(FakeFileSystem fs) =>
        new(new MvvmMapConfig(),
            new XamlParser(fs, NullLogger<XamlParser>.Instance),
            fs,
            NullLogger<CommandResolver>.Instance);

    [Fact]
    public async Task Resolve_RelayCommandInCtor_EmitsContainsEdge()
    {
        const string vmSource = """
            namespace MyApp.ViewModels;
            using System.Windows.Input;
            public class LoginViewModel
            {
                public ICommand LoginCommand { get; }
                public LoginViewModel() { LoginCommand = new RelayCommand(ExecuteLogin); }
                private void ExecuteLogin() { }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/ViewModels/LoginViewModel.cs"] = vmSource
        });

        var resolver = BuildResolver(fs);
        var discovery = new DiscoveryResult([], ["/root/ViewModels/LoginViewModel.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.Contains && e.Confidence == Confidence.High);
        var edge = result.Edges[0];
        edge.FromId.Should().Be("vm:MyApp.ViewModels.LoginViewModel");
        edge.ToId.Should().Be("method:MyApp.ViewModels.LoginViewModel.ExecuteLogin");
    }

    [Fact]
    public async Task Resolve_RelayCommandAttribute_EmitsContainsEdge()
    {
        const string vmSource = """
            namespace MyApp.ViewModels;
            public partial class LoginViewModel
            {
                [RelayCommand]
                private async System.Threading.Tasks.Task LoginAsync() { }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/ViewModels/LoginViewModel.cs"] = vmSource
        });

        var resolver = BuildResolver(fs);
        var discovery = new DiscoveryResult([], ["/root/ViewModels/LoginViewModel.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.Contains);
        var edge = result.Edges[0];
        edge.Reason.Should().Contain("LoginAsync → LoginCommand");
    }

    [Fact]
    public async Task Resolve_XamlCommandBinding_EmitsInvokesEdge()
    {
        const string vmSource = """
            namespace MyApp.ViewModels;
            using System.Windows.Input;
            public class LoginViewModel
            {
                public ICommand LoginCommand { get; }
                public LoginViewModel() { LoginCommand = new RelayCommand(ExecuteLogin); }
                private void ExecuteLogin() { }
            }
            """;

        const string xaml = """
            <UserControl x:Class="MyApp.Views.LoginView"
                         xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Button Command="{Binding LoginCommand}" Content="Login" />
            </UserControl>
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/ViewModels/LoginViewModel.cs"] = vmSource,
            ["/root/Views/LoginView.xaml"] = xaml
        });

        var resolver = BuildResolver(fs);
        var discovery = new DiscoveryResult(
            ["/root/Views/LoginView.xaml"],
            ["/root/ViewModels/LoginViewModel.cs"],
            "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().Contain(e => e.Kind == EdgeKind.Invokes && e.Confidence == Confidence.High);
        var invoke = result.Edges.First(e => e.Kind == EdgeKind.Invokes);
        invoke.FromId.Should().Be("view:MyApp.Views.LoginView");
        invoke.ToId.Should().Be("method:MyApp.ViewModels.LoginViewModel.ExecuteLogin");
    }

    [Fact]
    public async Task Resolve_ICommandPropertyNoExecute_EmitsMediumContainsEdge()
    {
        const string vmSource = """
            namespace MyApp.ViewModels;
            using System.Windows.Input;
            public class LoginViewModel
            {
                public ICommand LoginCommand { get; set; }
            }
            """;

        var fs = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/root/ViewModels/LoginViewModel.cs"] = vmSource
        });

        var resolver = BuildResolver(fs);
        var discovery = new DiscoveryResult([], ["/root/ViewModels/LoginViewModel.cs"], "/root");

        var result = await resolver.ResolveAsync(discovery);

        result.Edges.Should().ContainSingle(e => e.Kind == EdgeKind.Contains && e.Confidence == Confidence.Medium);
        result.Edges[0].Reason.Should().Contain("without resolved Execute");
    }
}
