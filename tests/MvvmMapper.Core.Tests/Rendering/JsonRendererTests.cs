using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Rendering;
using System.Text.Json;
using Xunit;

namespace MvvmMapper.Core.Tests.Rendering;

public sealed class JsonRendererTests
{
    [Fact]
    public async Task RenderAsync_WritesValidJson()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var renderer = new JsonRenderer(NullLogger<JsonRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        var outputFile = Path.Combine(outputDir, "graph.json");
        File.Exists(outputFile).Should().BeTrue();

        var json = await File.ReadAllTextAsync(outputFile);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nodes").GetArrayLength().Should().BeGreaterThan(0);
        doc.RootElement.GetProperty("edges").GetArrayLength().Should().BeGreaterThan(0);

        // Cleanup
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
                EdgeKind.BindsTo, Confidence.High, "Test edge")
        };
        return GraphTestHelpers.BuildGraph(nodes, edges);
    }
}
