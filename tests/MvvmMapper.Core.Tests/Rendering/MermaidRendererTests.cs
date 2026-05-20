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
