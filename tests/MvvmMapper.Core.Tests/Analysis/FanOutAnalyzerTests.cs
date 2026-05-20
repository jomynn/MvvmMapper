using FluentAssertions;
using MvvmMapper.Core.Analysis;
using MvvmMapper.Core.Graph;
using MvvmMapper.Core.Tests.Rendering;
using Xunit;

namespace MvvmMapper.Core.Tests.Analysis;

public sealed class FanOutAnalyzerTests
{
    [Fact]
    public void Analyze_SharedVm_DetectedWithCorrectFanIn()
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

        var analyzer = new FanOutAnalyzer();
        var result = analyzer.Analyze(graph);

        result.Should().ContainSingle();
        result[0].DisplayName.Should().Be("AuthViewModel");
        result[0].FanIn.Should().Be(3);
        result[0].BoundViewIds.Should().HaveCount(3);
    }

    [Fact]
    public void Analyze_NonSharedVm_NotReported()
    {
        var nodes = new Dictionary<string, Node>
        {
            ["view:v1"] = new ViewNode("view:v1", "LoginView", "lv.xaml", null, ""),
            ["vm:login"] = new ViewModelNode("vm:login", "LoginViewModel", "lvm.cs", null, "LoginViewModel"),
        };
        var edges = new List<Edge>
        {
            new("view:v1", "vm:login", EdgeKind.BindsTo, Confidence.High, "test"),
        };
        var graph = GraphTestHelpers.BuildGraph(nodes, edges);

        var analyzer = new FanOutAnalyzer();
        var result = analyzer.Analyze(graph);

        result.Should().BeEmpty();
    }
}
