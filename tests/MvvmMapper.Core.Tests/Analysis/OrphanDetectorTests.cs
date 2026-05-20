using FluentAssertions;
using MvvmMapper.Core.Analysis;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Tests.Rendering;
using Xunit;

namespace MvvmMapper.Core.Tests.Analysis;

public sealed class OrphanDetectorTests
{
    [Fact]
    public void Detect_ViewWithNoVm_IsOrphaned()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:LoginView"] = new ViewNode("view:LoginView", "LoginView", "lv.xaml", null, ""),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, []);

        var detector = new OrphanDetector();
        var (orphanViews, _, _) = detector.Detect(graph);

        orphanViews.Should().ContainSingle(o => o.NodeId == "view:LoginView");
    }

    [Fact]
    public void Detect_ViewWithVm_IsNotOrphaned()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:LoginView"] = new ViewNode("view:LoginView", "LoginView", "lv.xaml", null, ""),
            ["vm:LoginViewModel"] = new ViewModelNode("vm:LoginViewModel", "LoginViewModel", "lvm.cs", null, "LoginViewModel"),
        };
        var edges = new List<Edge>
        {
            new("view:LoginView", "vm:LoginViewModel", EdgeKind.BindsTo, Confidence.High, "test")
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);

        var detector = new OrphanDetector();
        var (orphanViews, _, _) = detector.Detect(graph);

        orphanViews.Should().BeEmpty();
    }

    [Fact]
    public void Detect_VmWithNoView_IsOrphaned()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["vm:StandaloneVm"] = new ViewModelNode("vm:StandaloneVm", "StandaloneVm", "svm.cs", null, "StandaloneVm"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, []);

        var detector = new OrphanDetector();
        var (_, orphanVMs, _) = detector.Detect(graph);

        orphanVMs.Should().ContainSingle(o => o.NodeId == "vm:StandaloneVm");
    }

    [Fact]
    public void Detect_EndpointWithNoHitsEdge_IsOrphanedEndpoint()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["endpoint:e1"] = new EndpointNode("endpoint:e1", "POST /api/login", "svc.cs", 5, "POST", "/api/login"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, []);

        var detector = new OrphanDetector();
        var (_, _, orphanEndpoints) = detector.Detect(graph);

        orphanEndpoints.Should().ContainSingle(o => o.NodeId == "endpoint:e1");
    }

    [Fact]
    public void Detect_EndpointWithHitsEdge_IsNotOrphaned()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["method:m1"] = new MethodNode("method:m1", "ExecuteLogin", "lvm.cs", 10, "LoginViewModel"),
            ["endpoint:e1"] = new EndpointNode("endpoint:e1", "POST /api/login", "svc.cs", 5, "POST", "/api/login"),
        };
        var edges = new List<Edge>
        {
            new("method:m1", "endpoint:e1", EdgeKind.Hits, Confidence.High, "test"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);

        var detector = new OrphanDetector();
        var (_, _, orphanEndpoints) = detector.Detect(graph);

        orphanEndpoints.Should().BeEmpty();
    }

    [Fact]
    public void Detect_EmptyGraph_ReturnsAllEmptyLists()
    {
        var graph = GraphTestHelpers.BuildGraph(new Dictionary<string, Node>(), []);

        var detector = new OrphanDetector();
        var (views, vms, endpoints) = detector.Detect(graph);

        views.Should().BeEmpty();
        vms.Should().BeEmpty();
        endpoints.Should().BeEmpty();
    }
}
