using FluentAssertions;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Tests.Rendering;
using Xunit;

namespace MvvmMapper.Core.Tests.Graph;

public sealed class MvvmGraphTests
{
    [Fact]
    public void EdgesFrom_ReturnsOnlyEdgesWithMatchingFromId()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
            ["vm:lvm"] = new ViewModelNode("vm:lvm", "LoginViewModel", "lvm.cs", null, "LoginViewModel"),
            ["view:v2"] = new ViewNode("view:v2", "OtherView", "ov.xaml", null, ""),
        };
        var edges = new List<Edge>
        {
            new("view:v1", "vm:lvm", EdgeKind.BindsTo, Confidence.High, "test1"),
            new("view:v2", "vm:lvm", EdgeKind.BindsTo, Confidence.Low, "test2"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);

        var fromV1 = graph.EdgesFrom("view:v1").ToList();
        fromV1.Should().ContainSingle(e => e.Reason == "test1");
    }

    [Fact]
    public void EdgesTo_ReturnsOnlyEdgesWithMatchingToId()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
            ["vm:lvm"] = new ViewModelNode("vm:lvm", "LoginViewModel", "lvm.cs", null, "LoginViewModel"),
            ["view:v2"] = new ViewNode("view:v2", "OtherView", "ov.xaml", null, ""),
        };
        var edges = new List<Edge>
        {
            new("view:v1", "vm:lvm", EdgeKind.BindsTo, Confidence.High, "test1"),
            new("view:v2", "vm:lvm", EdgeKind.BindsTo, Confidence.Low, "test2"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);

        var toVm = graph.EdgesTo("vm:lvm").ToList();
        toVm.Should().HaveCount(2);
    }

    [Fact]
    public void ReachableEndpoints_TraversesFullChain()
    {
        // View --BindsTo--> VM --Contains--> Method --Hits--> Endpoint
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
            ["vm:lvm"] = new ViewModelNode("vm:lvm", "LoginViewModel", "lvm.cs", null, "LoginViewModel"),
            ["method:m1"] = new MethodNode("method:m1", "ExecuteLogin", "lvm.cs", 10, "LoginViewModel"),
            ["endpoint:e1"] = new EndpointNode("endpoint:e1", "POST /api/login", "svc.cs", 5, "POST", "/api/login"),
        };
        var edges = new List<Edge>
        {
            new("view:v1", "vm:lvm", EdgeKind.BindsTo, Confidence.High, "test"),
            new("vm:lvm", "method:m1", EdgeKind.Contains, Confidence.High, "test"),
            new("method:m1", "endpoint:e1", EdgeKind.Hits, Confidence.High, "test"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);

        var endpoints = graph.ReachableEndpoints("view:v1").ToList();

        endpoints.Should().ContainSingle();
        endpoints[0].Route.Should().Be("/api/login");
        endpoints[0].Verb.Should().Be("POST");
    }

    [Fact]
    public void ReachableEndpoints_EmptyGraph_ReturnsEmpty()
    {
        var graph = GraphTestHelpers.BuildGraph(new Dictionary<string, Node>(), []);

        var endpoints = graph.ReachableEndpoints("view:nonexistent").ToList();

        endpoints.Should().BeEmpty();
    }

    [Fact]
    public void ReachableEndpoints_ViewWithNoEdges_ReturnsEmpty()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, []);

        var endpoints = graph.ReachableEndpoints("view:v1").ToList();

        endpoints.Should().BeEmpty();
    }
}
