using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MvvmMapper.Core.Analysis;
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

    [Fact]
    public async Task RenderAsync_WithAnalysisReportWithOrphans_HtmlContainsOrphanWarning()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var orphanView = new OrphanedNode("view:orphan", "OrphanView", "View");
        var report = new AnalysisReport(
            OrphanedViews: [orphanView],
            OrphanedViewModels: [],
            UnreachableEndpoints: [],
            SharedViewModels: [],
            EndpointImpacts: []);

        var renderer = new HtmlRenderer(NullLogger<HtmlRenderer>.Instance);
        renderer.AnalysisReport = report;
        await renderer.RenderAsync(graph, outputDir);

        var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "report.html"));
        html.Should().Contain("Orphan View");
        html.Should().Contain("OrphanView");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_WithAnalysisReportWithOrphanVm_HtmlContainsOrphanVmWarning()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var orphanVm = new OrphanedNode("vm:orphan", "OrphanViewModel", "ViewModel");
        var report = new AnalysisReport(
            OrphanedViews: [],
            OrphanedViewModels: [orphanVm],
            UnreachableEndpoints: [],
            SharedViewModels: [],
            EndpointImpacts: []);

        var renderer = new HtmlRenderer(NullLogger<HtmlRenderer>.Instance);
        renderer.AnalysisReport = report;
        await renderer.RenderAsync(graph, outputDir);

        var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "report.html"));
        html.Should().Contain("Orphan VM");
        html.Should().Contain("OrphanViewModel");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_WithAnalysisReportWithSharedVm_HtmlContainsSharedVmInfo()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var sharedVm = new SharedViewModelInfo("vm:auth", "AuthViewModel", 3, ["view:v1", "view:v2", "view:v3"]);
        var report = new AnalysisReport(
            OrphanedViews: [],
            OrphanedViewModels: [],
            UnreachableEndpoints: [],
            SharedViewModels: [sharedVm],
            EndpointImpacts: []);

        var renderer = new HtmlRenderer(NullLogger<HtmlRenderer>.Instance);
        renderer.AnalysisReport = report;
        await renderer.RenderAsync(graph, outputDir);

        var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "report.html"));
        html.Should().Contain("Shared VM");
        html.Should().Contain("AuthViewModel");
        html.Should().Contain("fan-in=3");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_WithEndpointNode_HtmlContainsEndpointRow()
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

        var renderer = new HtmlRenderer(NullLogger<HtmlRenderer>.Instance);
        await renderer.RenderAsync(graph, outputDir);

        var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "report.html"));
        html.Should().Contain("/api/auth/login");
        html.Should().Contain("POST");
        html.Should().Contain("LoginView");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_WithUnreachableEndpoint_HtmlContainsUnreachableEndpointWarning()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var unreachable = new OrphanedNode("endpoint:e1", "POST /api/orphan", "Endpoint");
        var report = new AnalysisReport(
            OrphanedViews: [],
            OrphanedViewModels: [],
            UnreachableEndpoints: [unreachable],
            SharedViewModels: [],
            EndpointImpacts: []);

        var renderer = new HtmlRenderer(NullLogger<HtmlRenderer>.Instance);
        renderer.AnalysisReport = report;
        await renderer.RenderAsync(graph, outputDir);

        var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "report.html"));
        html.Should().Contain("Unreachable Endpoint");
        html.Should().Contain("POST /api/orphan");

        Directory.Delete(outputDir, recursive: true);
    }

    [Fact]
    public async Task RenderAsync_NullAnalysisReport_HtmlDoesNotContainAnalysisSection()
    {
        var graph = BuildSimpleGraph();
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var renderer = new HtmlRenderer(NullLogger<HtmlRenderer>.Instance);
        // AnalysisReport is null by default — analysis section should be empty string
        await renderer.RenderAsync(graph, outputDir);

        var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "report.html"));
        html.Should().NotContain("Orphan View:");
        html.Should().NotContain("fffbe6"); // analysis section background color only present when analysis section renders

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
