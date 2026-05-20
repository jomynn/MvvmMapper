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
}
