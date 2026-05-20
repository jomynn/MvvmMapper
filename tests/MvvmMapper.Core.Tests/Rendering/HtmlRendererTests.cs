using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Rendering;
using Xunit;

namespace MvvmMapper.Core.Tests.Rendering;

public sealed class HtmlRendererTests
{
    [Fact]
    public async Task RenderAsync_WritesHtmlFile()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var renderer = new HtmlRenderer(NullLogger<HtmlRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        var outputFile = Path.Combine(outputDir, "report.html");
        File.Exists(outputFile).Should().BeTrue();

        var html = await File.ReadAllTextAsync(outputFile);
        html.Should().Contain("MVVM Map Report");
        html.Should().Contain("LoginView");
        html.Should().Contain("LoginViewModel");
        html.Should().NotContain("cdn.jsdelivr.net");
        html.Should().NotContain("unpkg.com");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_SharedVm_MarksFanIn()
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

        var renderer = new HtmlRenderer(NullLogger<HtmlRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "report.html"));
        html.Should().Contain("SHARED");
        html.Should().Contain("3"); // fan-in count

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
