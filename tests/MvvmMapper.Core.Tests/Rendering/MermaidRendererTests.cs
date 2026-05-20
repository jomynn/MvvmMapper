using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Rendering;
using Xunit;

namespace MvvmMapper.Core.Tests.Rendering;

public sealed class MermaidRendererTests
{
    [Fact]
    public async Task RenderAsync_WritesThreeMdFiles()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var renderer = new MermaidRenderer(NullLogger<MermaidRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        File.Exists(Path.Combine(outputDir, "mermaid-by-view.md")).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "mermaid-by-vm.md")).Should().BeTrue();
        File.Exists(Path.Combine(outputDir, "mermaid-by-endpoint.md")).Should().BeTrue();

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_ByViewFile_ContainsMermaidBlock()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var renderer = new MermaidRenderer(NullLogger<MermaidRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "mermaid-by-view.md"));
        content.Should().Contain("```mermaid");
        content.Should().Contain("flowchart LR");
        content.Should().Contain("LoginView");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_SharedVm_ByVmFileContainsSharedLabel()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
            ["view:v2"] = new ViewNode("view:v2", "RegisterView", "rv.xaml", null, ""),
            ["view:v3"] = new ViewNode("view:v3", "ForgotView", "fv.xaml", null, ""),
            ["vm:auth"] = new ViewModelNode("vm:auth", "AuthViewModel", "auth.cs", null, "AuthViewModel"),
        };
        var edges = new List<Edge>
        {
            new("view:v1", "vm:auth", EdgeKind.BindsTo, Confidence.High, "test"),
            new("view:v2", "vm:auth", EdgeKind.BindsTo, Confidence.High, "test"),
            new("view:v3", "vm:auth", EdgeKind.BindsTo, Confidence.High, "test"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var renderer = new MermaidRenderer(NullLogger<MermaidRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "mermaid-by-vm.md"));
        content.Should().Contain("[SHARED]");
        content.Should().Contain("AuthViewModel");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_WithEndpoint_ByEndpointFileContainsEndpointSection()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
            ["vm:lvm"] = new ViewModelNode("vm:lvm", "LoginViewModel", "lvm.cs", null, "LoginViewModel"),
            ["method:m1"] = new MethodNode("method:m1", "ExecuteLogin", "lvm.cs", 10, "LoginViewModel"),
            ["endpoint:e1"] = new EndpointNode("endpoint:e1", "POST /api/auth/login", "svc.cs", 5, "POST", "/api/auth/login"),
        };
        var edges = new List<Edge>
        {
            new("view:v1", "vm:lvm", EdgeKind.BindsTo, Confidence.High, "test"),
            new("vm:lvm", "method:m1", EdgeKind.Contains, Confidence.High, "test"),
            new("method:m1", "endpoint:e1", EdgeKind.Hits, Confidence.High, "test"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var renderer = new MermaidRenderer(NullLogger<MermaidRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "mermaid-by-endpoint.md"));
        content.Should().Contain("POST /api/auth/login");
        content.Should().Contain("flowchart RL");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_ViewWithEndpoint_ByViewFileContainsHitsEdge()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
            ["vm:lvm"] = new ViewModelNode("vm:lvm", "LoginViewModel", "lvm.cs", null, "LoginViewModel"),
            ["method:m1"] = new MethodNode("method:m1", "ExecuteLogin", "lvm.cs", 10, "LoginViewModel"),
            ["endpoint:e1"] = new EndpointNode("endpoint:e1", "POST /api/auth/login", "svc.cs", 5, "POST", "/api/auth/login"),
        };
        var edges = new List<Edge>
        {
            new("view:v1", "vm:lvm", EdgeKind.BindsTo, Confidence.High, "test"),
            new("vm:lvm", "method:m1", EdgeKind.Contains, Confidence.High, "test"),
            new("method:m1", "endpoint:e1", EdgeKind.Hits, Confidence.High, "test"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var renderer = new MermaidRenderer(NullLogger<MermaidRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "mermaid-by-view.md"));
        content.Should().Contain("Hits");
        content.Should().Contain("LoginView");

        Directory.Delete(outputDir, recursive: true);
    }

    private static MvvmGraph BuildSimpleGraph()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:MyApp.Views.LoginView"] = new ViewNode(
                "view:MyApp.Views.LoginView", "LoginView", "LoginView.xaml", null, "MyApp.Views.LoginView"),
            ["vm:MyApp.ViewModels.LoginViewModel"] = new ViewModelNode(
                "vm:MyApp.ViewModels.LoginViewModel", "LoginViewModel", "LoginViewModel.cs", null,
                "MyApp.ViewModels.LoginViewModel"),
        };
        var edges = new List<Edge>
        {
            new("view:MyApp.Views.LoginView", "vm:MyApp.ViewModels.LoginViewModel",
                EdgeKind.BindsTo, Confidence.High, "Explicit DataContext")
        };
        return GraphTestHelpers.BuildGraph(nodes, edges);
    }
}
