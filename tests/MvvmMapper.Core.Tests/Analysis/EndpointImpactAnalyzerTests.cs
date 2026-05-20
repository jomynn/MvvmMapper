using FluentAssertions;
using MvvmMapper.Core.Analysis;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Tests.Rendering;
using Xunit;

namespace MvvmMapper.Core.Tests.Analysis;

public sealed class EndpointImpactAnalyzerTests
{
    [Fact]
    public void Analyze_ViewReachesEndpointViaVmAndMethod_IsDetected()
    {
        // View --BindsTo--> VM --Contains--> Method --Hits--> Endpoint
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

        var analyzer = new EndpointImpactAnalyzer();
        var result = analyzer.Analyze(graph);

        result.Should().ContainSingle();
        result[0].Route.Should().Be("/api/auth/login");
        result[0].ReachableFromViewIds.Should().Contain("view:v1");
    }
}
