using FluentAssertions;
using MvvmMapper.Core.Analysis;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Tests.Rendering;
using Xunit;

namespace MvvmMapper.Core.Tests.Analysis;

public sealed class AnalysisRunnerTests
{
    private static AnalysisRunner CreateRunner() =>
        new(new OrphanDetector(), new FanOutAnalyzer(), new EndpointImpactAnalyzer());

    [Fact]
    public void Run_EmptyGraph_ReturnsNonNullReport()
    {
        var graph = GraphTestHelpers.BuildGraph(new Dictionary<string, Node>(), []);
        var runner = CreateRunner();

        var report = runner.Run(graph);

        report.Should().NotBeNull();
        report.OrphanedViews.Should().BeEmpty();
        report.OrphanedViewModels.Should().BeEmpty();
        report.UnreachableEndpoints.Should().BeEmpty();
        report.SharedViewModels.Should().BeEmpty();
        report.EndpointImpacts.Should().BeEmpty();
    }

    [Fact]
    public void Run_GraphWithOrphanView_ReportsOrphan()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, []);
        var runner = CreateRunner();

        var report = runner.Run(graph);

        report.OrphanedViews.Should().ContainSingle(o => o.NodeId == "view:v1");
    }

    [Fact]
    public void Run_SharedVm_ReportedCorrectly()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
            ["view:v2"] = new ViewNode("view:v2", "RegisterView", "rv.xaml", null, ""),
            ["vm:auth"] = new ViewModelNode("vm:auth", "AuthViewModel", "auth.cs", null, "AuthViewModel"),
        };
        var edges = new List<Edge>
        {
            new("view:v1", "vm:auth", EdgeKind.BindsTo, Confidence.High, "test"),
            new("view:v2", "vm:auth", EdgeKind.BindsTo, Confidence.High, "test"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);
        var runner = CreateRunner();

        var report = runner.Run(graph);

        report.SharedViewModels.Should().ContainSingle(s =>
            s.ViewModelId == "vm:auth" && s.FanIn == 2);
    }

    [Fact]
    public void Run_EndpointReachable_ReportedInImpacts()
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
        var runner = CreateRunner();

        var report = runner.Run(graph);

        report.EndpointImpacts.Should().ContainSingle(i =>
            i.Route == "/api/auth/login" &&
            i.ReachableFromViewIds.Contains("view:v1"));
    }
}
